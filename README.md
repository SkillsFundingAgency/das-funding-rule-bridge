## ⛔Never push sensitive information such as client id's, secrets or keys into repositories including in the README file⛔

# SFA.DAS.FundingRuleBridge

<img src="https://avatars.githubusercontent.com/u/9841374?s=200&v=4" align="right" alt="UK Government logo">

[![Build Status](https://dev.azure.com/sfa-gov-uk/Digital%20Apprenticeship%20Service/_apis/build/status/das-sfa-funding-rule-service-bridge?branchName=main)](https://dev.azure.com/sfa-gov-uk/Digital%20Apprenticeship%20Service/_build/latest?definitionId=_projectid_&branchName=main)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=_projectId_&metric=alert_status)](https://sonarcloud.io/dashboard?id=_projectId_)
[![Jira Project](https://img.shields.io/badge/Jira-Project-blue)](https://skillsfundingagency.atlassian.net/secure/RapidBoard.jspa?rapidView=564&projectKey=FAI)
[![License](https://img.shields.io/badge/license-MIT-lightgrey.svg?longCache=true&style=flat-square)](https://en.wikipedia.org/wiki/MIT_License)

An Azure Durable Functions service that acts as a bridge between the ILR (Individual Learner Record) processing pipeline and the funding rules validation service. It receives a job message pointing to a valid ILR file, fans out per-learner validation requests to an external service, collects the results, and reports job completion once all learners have been processed.

## How It Works

### Message Flow

```
[process-job queue]
        │
        ▼
ProcessJobTrigger
  Starts a ProcessJobOrchestrator instance.
  The Service Bus message is acknowledged immediately.
        │
        ▼
ProcessJobOrchestrator
  1. DownloadAndParseIlrActivity
       Downloads the ILR XML file from blob storage.
       Parses all <Learner> elements into a list of
       { LearnRefNumber, DateOfBirth }.
        │
        ▼
  2. Fan-out: one ValidateLearnerOrchestrator per learner (parallel)
       All learners are validated concurrently.
       An infrastructure error (e.g. timeout) fails the job and triggers retry.
       A business validation failure (IsValid: false) is a normal outcome
       and does not prevent other learners completing.
        │
        ▼
  Per learner — ValidateLearnerOrchestrator
    a. SendValidationRequestActivity
         Sends { LearnRefNumber, DateOfBirth, OrchestrationInstanceId }
         → [validate-learner-requests queue]
         (consumed by the external funding rules validation service —
          handled in a separate solution)
    b. WaitForExternalEvent("ValidationComplete")
         Durably paused until the external service responds.
        │
        ▼
  [validate-learner-requests-callback queue]
  ◄── External funding rules service posts callback message here
        │
        ▼
  ValidateLearnerCallbackTrigger
    Reads OrchestrationInstanceId from the callback message.
    Raises "ValidationComplete" event on the correct sub-orchestration.
    Sub-orchestration resumes with { LearnRefNumber, IsValid }.
        │
        ▼
  All sub-orchestrations complete (fan-in)
        │
        ▼
  3. SendJobCompleteActivity
       Sends { JobId, UkPrn, TotalLearners, ValidCount, InvalidCount }
       → [job-complete queue]
```

### Queues

| Queue | Direction | Purpose |
|---|---|---|
| `process-job` | Inbound | Triggers processing of a new ILR job |
| `validate-learner` | Outbound | Per-learner validation request to the external funding rules service |
| `validate-learner-callback` | Inbound | Per-learner response from the external funding rules service |
| `job-complete` | Outbound | Job summary once all learners are processed |

### Failure Semantics

| Scenario | Behaviour |
|---|---|
| Learner fails funding rule validation | `IsValid: false` recorded, processing continues for all other learners |
| Infrastructure error (timeout, SB unavailable) | Sub-orchestration throws, `Task.WhenAll` propagates, Durable Functions retries the job |

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
- **Azure Service Bus Emulator** on `localhost:5300` with all queues pre-configured
- **Azure SQL Edge** (backing store for the Service Bus emulator)
- **Azurite** (blob storage emulator) on `localhost:10000`

### Config

The function app reads configuration from `local.settings.json` when running locally.

| Key | Description |
|---|---|
| `AzureWebJobsStorage` | Storage connection for the Functions runtime (Azurite locally) |
| `ServiceBusConnection` | Connection string for the Service Bus namespace |
| `IlrBlobStorageConnection` | Connection string for the storage account holding ILR files |

`local.settings.json` example:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnection": "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "IlrBlobStorageConnection": "UseDevelopmentStorage=true",
    "EnvironmentName": "LOCAL"
  }
}
```

## 🔗 External Dependencies

* **ILR blob storage** — storage account containing valid ILR XML files. The container and filename are provided in the incoming `process-job` message.
* **Funding rules validation service** — external service that consumes from `validation-requests` and responds via `validate-learner-callback`. The callback message must include the `OrchestrationInstanceId` received in the request.

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

* LARS / course identifier is not yet included in the `validation-requests` message — tracked in FAI-3506.
