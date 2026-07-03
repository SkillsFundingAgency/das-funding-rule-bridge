namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record ValidationRequestMessage
{
    public string Uln { get; init; } = default!;
    public DateOnly DateOfBirth { get; init; }
    public List<Course> Courses { get; init; } = [];
    public string OrchestrationInstanceId { get; init; } = default!;
}
