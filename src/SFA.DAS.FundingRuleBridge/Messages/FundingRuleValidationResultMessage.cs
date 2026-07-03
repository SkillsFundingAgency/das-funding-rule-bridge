namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record FundingRuleValidationResultMessage
{
    public Guid LearnerId { get; init; }
}
