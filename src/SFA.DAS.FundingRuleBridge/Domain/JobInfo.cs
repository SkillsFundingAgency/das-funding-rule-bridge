namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public record JobInfo
{
    public long JobId { get; init; }
    public required string Ukprn { get; init; }
    public required string Container { get; init; }
    public required string ValidIlrXmlFilename { get; init; }
    public required string InvalidLearnerRefsFilename { get; init; }
    
    public string GetJobPath(string filename) => Path.Combine(Ukprn, JobId.ToString(), filename);
}