namespace SFA.DAS.FundingRuleBridge.Jobs.Core;

public static class GlobalConstants
{
    public const string IncomingJobQueue = "process-job";
    public const string OutgoingJobQueue = "job-complete";
    public const string ValidationRequestsQueue = "validate-learner-requests";
    public const string ValidationCallbackQueue = "validate-learner-callback";
}