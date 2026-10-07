namespace SFA.DAS.FundingRuleBridge.Jobs.Core;

public static class QueueConstants
{
    public const string IncomingJobQueue = "ASFundingValidation";
    public const string ExternalServiceBusConnectionString = "IncomingServiceBusConnection";
    public const string JobContextMessageTopicName = "ilr2627submissiontopic";
    public const string JobContextMessageSubscriptionName = "ASFundingValidation";
}