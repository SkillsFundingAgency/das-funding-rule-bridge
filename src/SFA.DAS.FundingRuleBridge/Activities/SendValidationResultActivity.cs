using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using System.Text.Json;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public class SendValidationResultActivity(ServiceBusClient serviceBusClient, ILogger<SendValidationResultActivity> logger)
{
    private readonly ServiceBusSender _sender = serviceBusClient.CreateSender("funding-rule-validation-result");

    [Function(nameof(SendValidationResultActivity))]
    public async Task Run(
        [ActivityTrigger] FundingRuleValidationResultMessage result,
        FunctionContext context)
    {
        var body = JsonSerializer.Serialize(result);
        await _sender.SendMessageAsync(new ServiceBusMessage(body));
        logger.LogInformation("Sent validation result for learner '{LearnerId}'.", result.LearnerId);
    }
}
