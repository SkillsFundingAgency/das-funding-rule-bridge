using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using SFA.DAS.FundingRuleBridge.Jobs.Activities;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

public class ValidateLearnerOrchestrator
{
    [Function(nameof(ValidateLearnerOrchestrator))]
    public static async Task<FundingRuleValidationResultMessage> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<ValidateLearnerMessage>()!;

        var request = new ValidationRequestMessage
        {
            LearnerId = input.LearnerId,
            OrchestrationInstanceId = context.InstanceId
        };

        await context.CallActivityAsync(nameof(SendValidationRequestActivity), request);

        var callback = await context.WaitForExternalEvent<ValidationCallbackMessage>("ValidationComplete");

        var result = new FundingRuleValidationResultMessage
        {
            LearnerId = callback.LearnerId
        };

        await context.CallActivityAsync(nameof(SendValidationResultActivity), result);

        return result;
    }
}
