using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public interface IRuleCheck
{
    string Name { get; }
    List<RuleCourseOutcome> Check(RuleData ruleData);
}
