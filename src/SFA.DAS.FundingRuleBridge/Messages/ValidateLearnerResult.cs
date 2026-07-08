using SFA.DAS.FundingRuleBridge.Jobs.Domain;

namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record ValidateLearnerResult(
    string CorrelationId,
    string Ukprn,
    string Uln,
    ValidationStatus Status,
    IEnumerable<RuleCourseOutcome> RuleOutcomes);