using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using System.Text.Json;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public class SendValidationRequestActivity(ServiceBusClient serviceBusClient, ILogger<SendValidationRequestActivity> logger)
{
    [Function(nameof(SendValidationRequestActivity))]
     public async Task Run(
        [ActivityTrigger] ValidationRequestMessage request,
        FunctionContext context)
    {
        var body = JsonSerializer.Serialize(request);
        
        await using var sender = serviceBusClient.CreateSender("validation-requests");
        await sender.SendMessageAsync(new ServiceBusMessage(body));
        logger.LogInformation("Sent validation request with correlationId '{CorrelationId}'.",
            request.CorrelationId);
    }
}
