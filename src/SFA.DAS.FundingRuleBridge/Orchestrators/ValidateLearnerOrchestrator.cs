using ESFA.DC.ILR.IO.Model.Validation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Activities;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

public class ValidateLearnerOrchestrator
{
    private const int ValidationTimeoutInHours = 1;
    
    [Function(nameof(ValidateLearnerOrchestrator))]
    public static async Task<ValidationSummary> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<ValidateLearnerOrchestrator>();
        var input = context.GetInput<ValidateLearnerMessage>()!;
        var request = new ValidationRequestMessage(
            input.Ukprn,
            input.Uln,
            input.Courses,
            input.CorrelationId,
            context.InstanceId
        );

        using (logger.BeginScope(new Dictionary<string, object> { { "SubInstanceId", context.InstanceId } }))
        {
            await context.CallActivityAsync(nameof(SendValidationRequestActivity), request);
            logger.LogInformation("Sent validation request, waiting for result");

            try
            {
                var validationResult = await context.WaitForExternalEvent<ValidateLearnerResult>("ValidationComplete", TimeSpan.FromHours(ValidationTimeoutInHours));
                logger.LogInformation("Received validation result");

                var failedOutcomes = validationResult.RuleOutcomes.Where(x => x.Outcome != RuleOutcome.Success).ToList();
                var failed = failedOutcomes.Select(x => new ValidationError
                    {
                        LearnerReferenceNumber = validationResult.Uln,
                        AimSequenceNumber = x.AimSequenceNumber,
                        RuleName = x.RuleName,
                        Severity = x.Outcome == RuleOutcome.Error ? "E" : "W",
                        ValidationErrorParameters = MapValidationErrorParameters(x.FundingRestrictions)
                    })
                    .ToList();
                var rules = failedOutcomes
                    .DistinctBy(x => x.RuleName)
                    .Select(x => new RuleDescriptionLookup(x.RuleName, x.RuleDescription))
                    .ToList();

                return new ValidationSummary(request.Uln, validationResult.Status, failed, rules);
            }
            catch (TaskCanceledException ex)
            {
                logger.LogError(ex, "Timed out waiting for validation result, marking as invalid");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception occured, marking as invalid");
            }
            
            // system failure
            return new ValidationSummary(request.Uln, ValidationStatus.SystemError, [], []);
        }
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
