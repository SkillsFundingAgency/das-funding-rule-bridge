namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record ProcessJobMessage
{
    public long JobId { get; init; }
    public ProcessJobKeyValues KeyValuePairs { get; init; } = default!;
}

public record ProcessJobKeyValues
{
    public string Ukprn { get; init; } = default!;
    public string Container { get; init; } = default!;
    public string Filename { get; init; } = default!;
    public string InvalidLearnRefNumbers { get; init; } = default!;
}