using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;
using System.Text.Json;
using SFA.DAS.FundingRuleBridge.Jobs.Core;

namespace SFA.DAS.FundingRuleBridge.Jobs.Endpoints;

public class ProcessJobEndpoint(ILogger<ProcessJobEndpoint> logger)
{
    [Function(nameof(ProcessJobTrigger))]
    public async Task ProcessJobTrigger(
        [ServiceBusTrigger(QueueConstants.IncomingJobQueue, Connection = QueueConstants.ExternalServiceBusConnectionString)] ServiceBusReceivedMessage message,
        [DurableClient] DurableTaskClient durableClient,
        FunctionContext executionContext)
    {
        ProcessJobMessage? job;
        try
        {
            job = JsonSerializer.Deserialize<ProcessJobMessage>(message.Body) ?? throw new Exception();
        }
        catch
        {
            throw new InvalidOperationException("Failed to deserialise ProcessJobMessage");
        }
        
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(ProcessJobOrchestrator), job, new StartOrchestrationOptions { InstanceId = message.CorrelationId });

        logger.LogInformation("Started orchestration '{InstanceId}' for job {JobId} (UkPrn: {UkPrn}, CorrelationId: {CorrelationId}).",
            instanceId, job.JobId, job.KeyValuePairs?.Ukprn, message.CorrelationId);
    }
}