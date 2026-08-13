using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using Newtonsoft.Json;
using SFA.DAS.FundingRuleBridge.Jobs.Core;
using JsonSerializer = System.Text.Json.JsonSerializer;

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
            callback = JsonSerializer.Deserialize<ValidateLearnerResult>(message.Body) ?? throw new JsonSerializationException();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to deserialise ValidateLearnerResult", ex);
        }
        
        await durableClient.RaiseEventAsync(callback.WaitingInstanceId, "ValidationComplete", callback, executionContext.CancellationToken);

        logger.LogInformation("Raised ValidationComplete event for orchestration '{InstanceId}' (CorrelationId: {CorrelationId}).",
            callback.CorrelationId, message.CorrelationId);
    }
}