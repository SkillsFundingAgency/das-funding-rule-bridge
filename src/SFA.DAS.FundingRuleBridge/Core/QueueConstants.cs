namespace SFA.DAS.FundingRuleBridge.Jobs.Core;

public static class QueueConstants
{
    public const string IncomingJobQueue = "ASFundingValidation";
    public const string OutgoingJobQueue = "jobstatusqueue";
    public const string ValidationRequestsQueue = "validate-learner-requests";
    public const string ValidationCallbackQueue = "validate-learner-callback";
    public const string ExternalBusKey = "ExternalServiceBus";
    public const string InternalBusKey = "InternalServiceBus";
    public const string ExternalServiceBusConnectionString = "IncomingServiceBusConnection";
    public const string InternalServiceBusConnectionString = "ServiceBusConnection";
}