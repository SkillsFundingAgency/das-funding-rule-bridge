namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record ValidationRequestMessage(string Ukprn, string Uln, IEnumerable<Course> Courses, string CorrelationId, string WaitingInstanceId);
