using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SFA.DAS.FundingRuleBridge.Jobs.Core;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public class SendValidationRequestActivity([FromKeyedServices(QueueConstants.InternalBusKey)] ServiceBusClient serviceBusClient)
{
    [Function(nameof(SendValidationRequestActivity))]
    public async Task Run([ActivityTrigger] ValidationRequestMessage request, FunctionContext context)
    {
        var body = JsonSerializer.Serialize(request);
        await using var sender = serviceBusClient.CreateSender(QueueConstants.ValidationRequestsQueue);
        await sender.SendMessageAsync(new ServiceBusMessage(body), context.CancellationToken);
    }
}