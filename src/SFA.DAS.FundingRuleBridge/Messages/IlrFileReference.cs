namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record IlrFileReference
{
    public string Container { get; init; } = default!;
    public string Filename { get; init; } = default!;
    public string ValidLearnRefNumbers { get; init; } = default!;
}
