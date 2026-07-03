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
            Uln = input.Uln,
            DateOfBirth = input.DateOfBirth,
            Courses = input.Courses,
            OrchestrationInstanceId = context.InstanceId
        };

        await context.CallActivityAsync(nameof(SendValidationRequestActivity), request);

        var callback = await context.WaitForExternalEvent<ValidationCallbackMessage>("ValidationComplete");

        return new FundingRuleValidationResultMessage
        {
            LearnRefNumber = callback.LearnRefNumber,
            IsValid = callback.IsValid
        };
    }
}
