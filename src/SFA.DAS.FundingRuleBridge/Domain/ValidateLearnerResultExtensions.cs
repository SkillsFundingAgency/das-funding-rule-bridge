using ESFA.DC.ILR.IO.Model.Validation;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public static class ValidateLearnerResultExtensions
{
    public static ValidationSummary ToValidationSummary(this ValidateLearnerResult result, string uln)
    {
        var failedOutcomes = result.RuleOutcomes.Where(x => x.Outcome != RuleOutcome.Success).ToList();

        var failed = failedOutcomes
            .Select(x => new ValidationError
            {
                LearnerReferenceNumber = result.Uln,
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

        return new ValidationSummary(uln, result.Status, failed, rules);
    }
    
    private static List<ValidationErrorParameter> MapValidationErrorParameters(IEnumerable<FundingRestriction> fundingRestrictions)
    {
        return fundingRestrictions
            .Where(x => x != FundingRestriction.Unknown)
            .Select(fundingRestriction => new ValidationErrorParameter
            {
                PropertyName = RestrictionPropertyNameMapper.Map(fundingRestriction.RestrictionName),
                Value = fundingRestriction.RestrictedValue
            })
            .ToList();
    }
}