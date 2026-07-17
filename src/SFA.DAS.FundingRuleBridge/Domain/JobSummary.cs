namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public class JobSummary
{
    public bool JobFailure { get; set; }
    public List<ValidationSummary> Items { get; set; } = [];
    public List<string> InvalidLearnerRefs { get; set; } = [];
}