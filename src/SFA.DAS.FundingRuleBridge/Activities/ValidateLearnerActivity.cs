using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Data;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public class ValidateLearnerActivity(
    IRulesRepository rulesRepository,
    IEnumerable<IRuleCheck> ruleChecks,
    ILogger<ValidateLearnerActivity> logger)
{
    [Function(nameof(ValidateLearnerActivity))]
    public async Task<ValidationSummary> Run([ActivityTrigger] ValidateLearnerMessage input, FunctionContext context)
    {
        try
        {
            var dates = input.Courses.Select(x => x.StartDate.Date).Distinct().ToList();
            var rules = await rulesRepository.GetActiveRulesForDatesAsync(dates, context.CancellationToken);

            if (rules is not { Count: > 0 })
            {
                logger.LogInformation("No active matching rules found");
                return new ValidationSummary(input.Uln, ValidationStatus.Passed, [], []);
            }

            var outputs = new List<RuleCourseOutcome>();
            foreach (var rule in rules)
            {
                var courses = input.Courses
                    .Where(x => x.StartDate >= rule.EffectiveFrom && x.StartDate <= rule.EffectiveTo)
                    .ToList();

                if (courses.Count == 0) continue;

                var ruleCheck = ruleChecks.FirstOrDefault(x => x.Name == rule.RuleName);
                if (ruleCheck is null)
                {
                    logger.LogWarning("No rule check found for rule {RuleName}", rule.RuleName);
                    continue;
                }

                var ruleInput = input with { Courses = courses };
                outputs.AddRange(ruleCheck.Check(new RuleData(rule, ruleInput)));
            }

            var status = outputs.Count == 0 || outputs.All(x => x.Outcome == RuleOutcome.Success)
                ? ValidationStatus.Passed
                : ValidationStatus.Failed;

            var result = new ValidateLearnerResult(string.Empty, string.Empty, input.Ukprn, input.Uln, status, outputs);
            return result.ToValidationSummary(input.Uln);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Learner validation failed");
            return new ValidationSummary(input.Uln, ValidationStatus.SystemError, [], []);
        }
    }
}
