using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SFA.DAS.FundingRuleBridge.Jobs.Core;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public class SendJobCompleteActivity(
    [FromKeyedServices(QueueConstants.ExternalBusKey)] ServiceBusClient serviceBusClient,
    ILogger<SendJobCompleteActivity> logger)
{
    [Function(nameof(SendJobCompleteActivity))]
    public async Task Run(
        [ActivityTrigger] JobCompleteMessage message,
        FunctionContext context)
    {
        var body = JsonSerializer.Serialize(message);
        await using var sender = serviceBusClient.CreateSender(QueueConstants.OutgoingJobQueue);
        await sender.SendMessageAsync(new ServiceBusMessage(body), context.CancellationToken);
        logger.LogInformation("Sent job-complete message for job {JobId}.", message.JobId);
    }
}