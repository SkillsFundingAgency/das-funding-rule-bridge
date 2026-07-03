using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;
using System.Text.Json;

namespace SFA.DAS.FundingRuleBridge.Jobs.Endpoints;

public class ValidateLearnerEndpoint(ILogger<ValidateLearnerEndpoint> logger)
{
    [Function(nameof(ValidateLearnerTrigger))]
    public async Task ValidateLearnerTrigger(
        [ServiceBusTrigger("validate-learner", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        [DurableClient] DurableTaskClient durableClient,
        FunctionContext executionContext)
    {
        var input = JsonSerializer.Deserialize<ValidateLearnerMessage>(message.Body)
            ?? throw new InvalidOperationException("Failed to deserialise ValidateLearnerMessage.");

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(ValidateLearnerOrchestrator), input);

        logger.LogInformation("Started orchestration '{InstanceId}' for job {JobId} (CorrelationId: {CorrelationId}).",
            instanceId, input.JobId, message.CorrelationId);
    }
}
