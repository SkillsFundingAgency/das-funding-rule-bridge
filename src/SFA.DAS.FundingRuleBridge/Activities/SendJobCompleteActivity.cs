using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using System.Text.Json;
using SFA.DAS.FundingRuleBridge.Jobs.Core;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public class SendJobCompleteActivity(ServiceBusClient serviceBusClient, ILogger<SendJobCompleteActivity> logger)
{
    [Function(nameof(SendJobCompleteActivity))]
    public async Task Run(
        [ActivityTrigger] JobCompleteMessage message,
        FunctionContext context)
    {
        var body = JsonSerializer.Serialize(message);
        await using var sender = serviceBusClient.CreateSender(GlobalConstants.OutgoingJobQueue);
        await sender.SendMessageAsync(new ServiceBusMessage(body));
        logger.LogInformation("Sent job-complete message for job {JobId}.", message.JobId);
    }
}
