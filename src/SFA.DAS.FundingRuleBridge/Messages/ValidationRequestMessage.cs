namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record ValidationRequestMessage
{
    public Guid LearnerId { get; init; }
    public string OrchestrationInstanceId { get; init; } = default!;
}
