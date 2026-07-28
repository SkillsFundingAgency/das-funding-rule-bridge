## ⛔Never push sensitive information such as client id's, secrets or keys into repositories including in the README file⛔

# SFA.DAS.FundingRuleBridge

<img src="https://avatars.githubusercontent.com/u/9841374?s=200&v=4" align="right" alt="UK Government logo">

[![Build Status](https://dev.azure.com/sfa-gov-uk/Digital%20Apprenticeship%20Service/_apis/build/status/das-sfa-funding-rule-service-bridge?branchName=main)](https://dev.azure.com/sfa-gov-uk/Digital%20Apprenticeship%20Service/_build/latest?definitionId=_projectid_&branchName=main)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=_projectId_&metric=alert_status)](https://sonarcloud.io/dashboard?id=_projectId_)
[![Jira Project](https://img.shields.io/badge/Jira-Project-blue)](https://skillsfundingagency.atlassian.net/secure/RapidBoard.jspa?rapidView=564&projectKey=FAI)
[![License](https://img.shields.io/badge/license-MIT-lightgrey.svg?longCache=true&style=flat-square)](https://en.wikipedia.org/wiki/MIT_License)

An Azure Durable Functions service that acts as a bridge between the ILR (Individual Learner Record) processing pipeline and the funding rules validation service. It receives a job message pointing to a valid ILR file, fans out per-learner validation requests to an external service, collects the results, and writes output files once all learners have been processed.

## How It Works

### Message Flow

```
[ilr2627submissiontopic / ASFundingValidation subscription]  ← InternalServiceBusConnection
        │
        ▼
JobContextMessageEndpoint  (AutoCompleteMessages = false)
  Deserialises JobContextMessage.
  Starts or resumes a ProcessJobOrchestrator instance.
  Waits synchronously for the orchestration to finish,
  then completes or abandons the message.
        │
        ▼
ProcessJobOrchestrator
  1. DownloadAndParseIlrActivity
       Downloads the ILR XML file from blob storage (IlrBlobStorageConnection).
       Filters to learners with relevant apprenticeships or short courses:
         - FundModel = Apprenticeships, ProgType = ApprenticeshipStandard, AimType = ProgrammeAim
         - OR learners with a Restart FAM
         - OR FundModel = NonFunded, ProgType = GrowthAndSkillsOfferApprenticeshipUnits
       Returns a list of { Uln, DateOfBirth, Courses[] } per learner.
        │
        ▼
  2. Fan-out: one ValidateLearnerOrchestrator per learner (parallel)
        │
        ▼
  Per learner — ValidateLearnerOrchestrator  (1-hour timeout)
    a. SendValidationRequestActivity
         Sends ValidationRequestMessage { Ukprn, Uln, Courses[], CorrelationId, WaitingInstanceId }
         → [validate-learner-requests queue]  (via ServiceBusClient, InternalServiceBusConnection)
         (consumed by the external funding rules validation service)
    b. WaitForExternalEvent("ValidationComplete", timeout: 1 hour)
         Durably paused until the external service responds.
         On timeout or unhandled exception → returns ValidationStatus.SystemError.
        │
        ▼
  [validate-learner-callback queue]  ← InternalServiceBusConnection
  ◄── External funding rules service posts ValidateLearnerResult callback here
        │
        ▼
  ValidateLearnerCallbackEndpoint
    Deserialises ValidateLearnerResult { WaitingInstanceId, Uln, Status, RuleOutcomes[] }.
    Raises "ValidationComplete" event on the correct sub-orchestration.
        │
        ▼
  All sub-orchestrations complete (fan-in)
        │
        ▼
  3. If any learner has ValidationStatus.SystemError → return false
       Message is abandoned (increments delivery count / goes to DLQ).
     If all learners are Passed or Failed →
       WriteJobsResultsActivity (only if there are invalid learners)
         Writes to blob storage (IlrBlobStorageConnection):
           {Ukprn}/{JobId}/ASValidationErrors.json
           {Ukprn}/{JobId}/ASInvalidLearnRefNumbers.json
           {Ukprn}/{JobId}/ASValidationErrorLookups.json
         Updates the ILR XML file to remove invalid learners.
       Message is completed.
```

### Queues and Topics

Two Service Bus namespaces are in play. The **internal bus** is owned by this service; the **external bus** is the shared SLD namespace used to communicate with the upstream DC pipeline.

| Resource | Type | Direction | Connection | Purpose |
|---|---|---|---|---|
| `ilr2627submissiontopic` / `ASFundingValidation` | Topic/Sub | Inbound | `InternalServiceBusConnection` | Receives job trigger from SLD |
| `validate-learner-requests` | Queue | Outbound | `InternalServiceBusConnection` | Per-learner validation request to the funding rules service |
| `validate-learner-callback` | Queue | Inbound | `InternalServiceBusConnection` | Per-learner response from the funding rules service |
| SLD topic (name from `SLDTopic:TopicName`) | Topic | Outbound | `IncomingServiceBusConnection` | Publishes JobContextDto back to the DC pipeline |
| Job status queue (name from `SLDTopic:JobStatusQueueName`) | Queue | Outbound | `SLDTopic:ServiceBusConnection` | Job status updates to the DC pipeline |
| Audit queue (name from `SLDTopic:AuditQueueName`) | Queue | Outbound | `SLDTopic:ServiceBusConnection` | Audit records to the DC pipeline |

> `SLDTopic:*` values come from Azure Table Storage configuration, not app settings.

### Failure Semantics

| Scenario | Behaviour |
|---|---|
| Learner fails funding rule validation (`ValidationStatus.Failed`) | Recorded as invalid, processing continues for all other learners |
| Infrastructure / timeout (`ValidationStatus.SystemError`) | Sub-orchestration returns SystemError; `ProcessJobOrchestrator` returns false; message is abandoned |
| `JobContextMessageEndpoint` unhandled exception | Message is abandoned with exception type recorded in `ApplicationProperties["Exceptions"]` |

### Idempotency

`JobContextMessageHandler` checks the Durable Functions instance state before starting a new orchestration:

- **Completed successfully** → completes the message immediately (no re-processing)
- **Running / pending / suspended** → waits for the existing instance to finish
- **Failed / terminated** → restarts the orchestration

## 🚀 Installation

### Pre-Requisites

* A clone of this repository
* .NET 10 SDK
* Docker Desktop (for local Service Bus emulator and Azurite blob storage)
* Azure Functions Core Tools v4

### Local Infrastructure

Start the local Service Bus emulator and Azurite blob storage with Docker Compose:

```bash
docker-compose up -d
```

This starts:
- **Azure Service Bus Emulator** on `localhost:5672` with all queues pre-configured
- **Azure SQL Edge** (backing store for the Service Bus emulator)
- **Azurite** (blob storage emulator) on `localhost:10000`

### Config

The function app reads configuration from `local.settings.json` when running locally, then from Azure Table Storage (keyed by `ConfigNames`).

#### Connection string names

| Key | Used by | Local format | Production format |
|---|---|---|---|
| `ServiceBusConnection__fullyQualifiedNamespace` | `JobContextMessageEndpoint` trigger, `ValidateLearnerCallbackEndpoint` trigger, `SendValidationRequestActivity` (`ServiceBusClient`) | N/A — use full SAS string in `ServiceBusConnection` locally | FQDN (Managed Identity) |
| `IncomingServiceBusConnection` | `TopicPublishService<JobContextDto>` (publishes to SLD topic) | Full SAS connection string | Full SAS connection string or FQDN |
| `IlrBlobStorageConnection` | `IlrBlobStorageClient` (blob download/upload) | `UseDevelopmentStorage=true` | Storage account connection string |
| `SLDTopic:ServiceBusConnection` | `QueuePublishService<JobStatusDto>`, `QueuePublishService<AuditingDto>` | From Table Storage | From Table Storage |
| `AzureWebJobsStorage` | Functions runtime / Durable Functions state | `UseDevelopmentStorage=true` | Storage account connection string |

#### Known connection string issues

**`ServiceBusClient` DI registration**

The internal `ServiceBusClient` (used by `SendValidationRequestActivity`) is registered in `HostBuilderExtensions.cs` to handle both environments:

- **Production**: reads `ServiceBusConnection__fullyQualifiedNamespace` (mapped in .NET IConfiguration as `ServiceBusConnection:fullyQualifiedNamespace`) and constructs `new ServiceBusClient(fqdn, new DefaultAzureCredential())` — Managed Identity.
- **Local**: `ServiceBusConnection__fullyQualifiedNamespace` is not set, so falls back to the full SAS connection string in `ServiceBusConnection` and constructs `new ServiceBusClient(connectionString)`.

**`IncomingServiceBusConnection` not in ARM template**

The ARM template does not set `IncomingServiceBusConnection`. This value must therefore be present in Azure Table Storage configuration at the root level (not under `SLDTopic:`).

`local.settings.json` example:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ConfigurationStorageConnectionString": "UseDevelopmentStorage=true;",
    "ConfigNames": "SFA.DAS.FundingRuleBridge.Jobs",
    "EnvironmentName": "LOCAL",
    "ResourceEnvironmentName": "LOCAL",
    "Version": "2.0",
    "ServiceBusConnection": "Endpoint=sb://localhost:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "IncomingServiceBusConnection": "Endpoint=sb://localhost:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "IlrBlobStorageConnection": "UseDevelopmentStorage=true"
  }
}
```

## 🔗 External Dependencies

* **ILR blob storage** — storage account containing valid ILR XML files. The container and filename are provided in the incoming `JobContextMessage`.
* **Funding rules validation service** — external service that consumes from `validate-learner-requests` and responds via `validate-learner-callback`. The callback message must include `WaitingInstanceId` from the request.
* **DC pipeline (SLD)** — upstream system that publishes to `ilr2627submissiontopic` and receives job status/audit messages back via the SLD queues.

## Technologies

* .NET 10
* Azure Functions V4 (isolated worker)
* Azure Durable Functions
* Azure Service Bus
* Azure Blob Storage
* NUnit
* Moq
* FluentAssertions

## 🐛 Known Issues

* `IncomingServiceBusConnection` is absent from the ARM template; it must be present in Table Storage config at the root level.
* `IncomingServiceBusConnection` is absent from the ARM template; it must be present in Table Storage config at the root level.
