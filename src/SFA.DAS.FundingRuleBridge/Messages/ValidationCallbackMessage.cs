namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record ValidationCallbackMessage
{
    public string LearnRefNumber { get; init; } = default!;
    public string OrchestrationInstanceId { get; init; } = default!;
    public bool IsValid { get; init; }
    // TODO: will be picked up as part of FAI-3506 what to send for the violation report
}
