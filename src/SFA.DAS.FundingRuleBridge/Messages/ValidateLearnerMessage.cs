namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record ValidateLearnerMessage
{
    public Guid LearnerId { get; init; }
}
