namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public record FundingRestriction(string RestrictionName, string RestrictedValue)
{
    public static FundingRestriction Unknown => new("Unknown", "Unknown");
}