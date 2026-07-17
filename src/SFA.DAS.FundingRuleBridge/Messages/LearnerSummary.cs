namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record LearnerSummary
{
    public string LearnRefNumber { get; init; } = default!;
    public DateOnly DateOfBirth { get; init; }
    public List<Course> Courses { get; init; } = [];
}
