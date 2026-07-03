using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using System.Text.Json;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public class SendJobCompleteActivity(ServiceBusClient serviceBusClient, ILogger<SendJobCompleteActivity> logger)
{
    private readonly ServiceBusSender _sender = serviceBusClient.CreateSender("job-complete");

    [Function(nameof(SendJobCompleteActivity))]
    public async Task Run(
        [ActivityTrigger] JobCompleteMessage message,
        FunctionContext context)
    {
        var body = JsonSerializer.Serialize(message);
        await _sender.SendMessageAsync(new ServiceBusMessage(body));
        logger.LogInformation("Sent job-complete message for job {JobId}.", message.JobId);
    }
}
