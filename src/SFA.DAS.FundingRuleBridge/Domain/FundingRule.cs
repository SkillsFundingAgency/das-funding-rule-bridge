namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public class FundingRule
{
    public required Guid Id { get; set; }
    public required string RuleName { get; set; }
    public required string IlrRuleName { get; set; }
    public required string IlrRuleDescription { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Parameters { get; set; } = default!;
    public HashSet<string> CourseIds { get; set; } = [];
}
