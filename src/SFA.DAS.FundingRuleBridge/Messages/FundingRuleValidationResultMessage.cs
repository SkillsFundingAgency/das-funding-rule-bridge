namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record FundingRuleValidationResultMessage
{
    public string LearnRefNumber { get; init; } = default!;
    public bool IsValid { get; init; }
}
