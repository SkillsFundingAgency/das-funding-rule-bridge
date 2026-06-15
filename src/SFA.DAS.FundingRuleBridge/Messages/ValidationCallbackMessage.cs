namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record ValidationCallbackMessage
{
    public Guid LearnerId { get; init; }
    public string OrchestrationInstanceId { get; init; } = default!;
}
