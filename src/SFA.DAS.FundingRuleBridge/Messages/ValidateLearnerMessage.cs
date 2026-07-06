namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record ValidateLearnerMessage
{
    public long JobId { get; init; }
    public string CorrelationId { get; init; } = default!;
    public string Ukprn { get; init; } = default!;
    public string Uln { get; init; } = default!;
    public DateOnly DateOfBirth { get; init; }
    public List<Course> Courses { get; init; } = [];
    public string Container { get; init; } = default!;
    public string Filename { get; init; } = default!;
}
