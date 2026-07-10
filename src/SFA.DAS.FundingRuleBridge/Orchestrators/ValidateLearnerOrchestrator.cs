using ESFA.DC.ILR.IO.Model.Validation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using SFA.DAS.FundingRuleBridge.Jobs.Activities;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

public class ValidateLearnerOrchestrator
{
    [Function(nameof(ValidateLearnerOrchestrator))]
    public static async Task<ValidationSummary> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<ValidateLearnerMessage>()!;

        var request = new ValidationRequestMessage(
            input.Ukprn,
            input.Uln,
            input.Courses,
            input.CorrelationId,
            context.InstanceId
        );

        await context.CallActivityAsync(nameof(SendValidationRequestActivity), request);
        var validationResult = await context.WaitForExternalEvent<ValidateLearnerResult>("ValidationComplete");

        var failed = validationResult.RuleOutcomes
            .Where(x => x.Outcome != RuleOutcome.Success)
            .Select(x => new ValidationError
            {
                LearnerReferenceNumber = validationResult.Uln,
                AimSequenceNumber = x.AimSequenceNumber,
                RuleName = x.RuleName,
                Severity = "E",
                ValidationErrorParameters = MapValidationErrorParameters(x.FundingRestrictions)
            })
            .ToList();

        var isValid = validationResult.RuleOutcomes.All(x => x.Outcome == RuleOutcome.Success);
        return new ValidationSummary(isValid, failed);
    }

    private static List<ValidationErrorParameter> MapValidationErrorParameters(IEnumerable<FundingRestriction> fundingRestrictions)
    {
        return fundingRestrictions
            .Where(x => x != FundingRestriction.Unknown)
            .Select(fundingRestriction =>
                new ValidationErrorParameter
                {
                    PropertyName = ValidationErrorParameterPropertyNameMapper.Map(fundingRestriction.RestrictionName),
                    Value = fundingRestriction.RestrictedValue
                })
            .ToList();
    }
}
