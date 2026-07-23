using ESFA.DC.JobContext.Interface;
using ESFA.DC.Queueing.Interface;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public interface IMessageHandler
{
    Task<IQueueCallbackResult> HandleAsync(JobContextDto jobContextDto, CancellationToken cancellationToken);
}