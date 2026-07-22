using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using System.Text.Json;
using SFA.DAS.FundingRuleBridge.Jobs.Core;

namespace SFA.DAS.FundingRuleBridge.Jobs.Endpoints;

public class ValidateLearnerCallbackEndpoint(ILogger<ValidateLearnerCallbackEndpoint> logger)
{
    [Function(nameof(ValidateLearnerCallbackTrigger))]
    public async Task ValidateLearnerCallbackTrigger(
        [ServiceBusTrigger(QueueConstants.ValidationCallbackQueue, Connection = QueueConstants.InternalServiceBusConnectionString)] ServiceBusReceivedMessage message,
        [DurableClient] DurableTaskClient durableClient,
        FunctionContext executionContext)
    {
        ValidateLearnerResult? callback;
        try
        {
            callback = JsonSerializer.Deserialize<ValidateLearnerResult>(message.Body) ?? throw new Exception();
        }
        catch
        {
            throw new InvalidOperationException("Failed to deserialise ValidateLearnerResult");
        }
        
        await durableClient.RaiseEventAsync(callback.WaitingInstanceId, "ValidationComplete", callback);

        logger.LogInformation("Raised ValidationComplete event for orchestration '{InstanceId}' (CorrelationId: {CorrelationId}).",
            callback.CorrelationId, message.CorrelationId);
    }
}