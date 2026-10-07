using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public record RuleData(FundingRule Rule, ValidateLearnerMessage Command);
