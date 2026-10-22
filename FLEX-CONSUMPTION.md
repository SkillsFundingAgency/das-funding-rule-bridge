# Flex Consumption hosting (DASD-16646)

The Bridge can be deployed as a second, separate function app on the Azure Functions
[Flex Consumption plan](https://learn.microsoft.com/azure/azure-functions/flex-consumption-plan).
Azure doesn't support moving an existing app to Flex in place, so this runs side by side with the
existing App Service plan app until cutover. None of the existing resources, templates or pipelines are changed.

| File | Purpose |
|---|---|
| `azure/template.flex.json` | Subscription-level ARM template for the Flex app and its supporting resources |
| `pipeline-templates/job/deploy-flex.yml` | ARM deploy, then `AzureFunctionApp@2` with `isFlexConsumption: true` |
| `azure-pipelines-flex.yml` | Manual pipeline: build, then deploy to AT, TEST, TEST2 and DEMO |

## What gets created per environment

| Resource | Name |
|---|---|
| Resource group | `das-<env>-fnrbwkr-flex-rg` |
| Flex Consumption plan (FC1, Linux) | `das-<env>-fnrbwkr-flex-asp` |
| Function app (.NET 10 isolated) | `das-<env>-fnrbwkr-flex-fa` |
| Storage account (host storage, Durable task hub, deployment package) | `das<env>fnrbwkrflexstr` |
| Application Insights | `das-<env>-fnrbwkr-flex-fa` |
| Subnet in the shared vnet, delegated to `Microsoft.App/environments` | `fnrbwkr-flex-sn` |
| Role assignment: Service Bus Data Owner on the shared namespace | |

The Durable task hub lives in the app's own storage account, not the shared one, as
[Microsoft recommends for Durable Functions](https://learn.microsoft.com/azure/azure-functions/storage-considerations#storage-account-guidance).

## Before the first run

1. **Register the `Microsoft.App` resource provider** in each subscription. It's currently `NotRegistered` in SFA-DAS-Dev/Test,
   and Flex VNet integration needs it.
2. **Confirm the subnet range** with the platform team. `azure-pipelines-flex.yml` proposes `<prefix>.233.0/24`,
   which was free in the AT, TEST, TEST2 and DEMO vnets on 22/10/2026.
3. **Create the pipeline** in Azure DevOps from `azure-pipelines-flex.yml`. It has no CI trigger.
4. **Check Service Bus rights.** For target-based scaling on the DC topic subscription, the `IncomingServiceBusConnection`
   connection string needs *Manage* rights. With *Listen* only, scaling falls back to incremental. That namespace is owned by DC.

## Scaling settings

All settings are template parameters. Each can be overridden with a pipeline variable of the same name.

| Parameter | Default | Notes |
|---|---|---|
| `flexMaximumInstanceCount` | 20 | Per function scale group. TBC on the ticket |
| `flexInstanceMemoryMB` | 2048 | 512, 2048 or 4096 |
| `flexServiceBusMaxConcurrentCalls` | 16 | Messages per instance. TBC on the ticket |
| `flexDurablePartitionCount` | 16 | Caps orchestrator scale-out. Fixed once the task hub exists |
| `flexDurableMaxQueuePollingInterval` | `00:00:10` | Microsoft recommends 10 seconds or less on Flex |
| `flexAlwaysReadyDurableInstances` | 0 | Microsoft recommends 1 for Durable Functions; 0 keeps lower environments scale-to-zero |
| `flexTriggersEnabled` | `false` | See cutover below |

The host.json values are applied as `AzureFunctionsJobHost__*` app settings on the Flex app only. That leaves `host.json`, and the
existing task hub's partition count, unchanged. The Flex app also sets `functionTimeout` to `01:00:00`, to match the hour of
message lock renewal in host.json, because `JobContextMessageEndpoint` holds its message until the whole ILR job finishes.

## Cutover

The Flex app is deployed with `JobContextMessageEndpoint` and `ValidateLearnerCallbackTrigger` **disabled**, because it consumes the same
topic subscription and callback queue as the existing app. A callback picked up by the wrong app would be lost, since each app
has its own task hub. To switch an environment over:

1. Wait for in-flight ILR jobs to finish.
2. Stop `das-<env>-fnrbwkr-fa`.
3. Run the Flex pipeline with `flexTriggersEnabled` set to `true`.

Both triggers must move together. Roll back by reversing the steps.

## Known follow-ups

- `JobContextMessageEndpoint` blocks for the whole job. On Flex, that wait is billed, and executions get at most 60 minutes to finish
  during scale-in. Completing the DC job from the orchestration, instead of holding the message, would remove this.
- PP, PROD and MO stages are deliberately not included yet.
