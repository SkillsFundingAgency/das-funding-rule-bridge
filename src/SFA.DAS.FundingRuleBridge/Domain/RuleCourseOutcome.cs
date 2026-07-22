namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public record RuleCourseOutcome(
    Guid RuleId,
    string RuleName,
    string RuleDescription,
    string CourseId,
    int AimSequenceNumber,
    RuleOutcome Outcome,
    IEnumerable<FundingRestriction> FundingRestrictions);