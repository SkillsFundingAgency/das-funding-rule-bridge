namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public static class ValidationSummaryExtensions
{
    public static JobSummary ToJobSummary(this IEnumerable<ValidationSummary> validationSummaries)
    {
        var items = validationSummaries.ToList();
        var failedValidation = items.Where(x => x.Status == ValidationStatus.Failed).ToList();
        return new JobSummary
        {
            JobFailure = items.Any(x => x.Status == ValidationStatus.SystemError),
            Items = items,
            InvalidLearnerRefs = failedValidation.Select(x => x.Uln).Distinct().ToList(),
            RuleDescriptions = items.SelectMany(x => x.RuleDescriptions).Distinct().ToList(),
        };
    }
}