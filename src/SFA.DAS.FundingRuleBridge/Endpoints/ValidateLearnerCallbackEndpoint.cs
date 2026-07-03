using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using System.Text.Json;

namespace SFA.DAS.FundingRuleBridge.Jobs.Endpoints;

public class ValidateLearnerCallbackEndpoint(ILogger<ValidateLearnerCallbackEndpoint> logger)
{
    [Function(nameof(ValidateLearnerCallbackTrigger))]
    public async Task ValidateLearnerCallbackTrigger(
        [ServiceBusTrigger("validate-learner-callback", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        [DurableClient] DurableTaskClient durableClient,
        FunctionContext executionContext)
    {
        var callback = JsonSerializer.Deserialize<ValidationCallbackMessage>(message.Body)
            ?? throw new InvalidOperationException("Failed to deserialise ValidationCallbackMessage.");

        await durableClient.RaiseEventAsync(callback.OrchestrationInstanceId, "ValidationComplete", callback);

        logger.LogInformation("Raised ValidationComplete event for orchestration '{InstanceId}' (CorrelationId: {CorrelationId}).",
            callback.OrchestrationInstanceId, message.CorrelationId);
    }
}
