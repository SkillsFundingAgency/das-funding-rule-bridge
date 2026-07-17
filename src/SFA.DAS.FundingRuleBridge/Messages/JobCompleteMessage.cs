namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record JobCompleteMessage
{
    public long JobId { get; init; }
    public string Ukprn { get; init; } = default!;
    public int TotalLearners { get; init; }
    public int ValidCount { get; init; }
    public int InvalidCount { get; init; }
}
