namespace SFA.DAS.FundingRuleBridge.Jobs.Core;

public static class QueueConstants
{
    public const string IncomingJobQueue = "process-job";
    public const string OutgoingJobQueue = "job-complete";
    public const string ValidationRequestsQueue = "validate-learner-requests";
    public const string ValidationCallbackQueue = "validate-learner-callback";
    public const string ExternalBusKey = "ExternalServiceBus";
    public const string InternalBusKey = "InternalServiceBus";
    public const string ExternalServiceBusConnectionString = "IncomingServiceBusConnection";
    public const string InternalServiceBusConnectionString = "ServiceBusConnection";
}