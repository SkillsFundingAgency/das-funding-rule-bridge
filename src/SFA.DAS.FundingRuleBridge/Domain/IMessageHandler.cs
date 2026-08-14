using ESFA.DC.JobContext.Interface;
using ESFA.DC.Queueing.Interface;
using Microsoft.DurableTask.Client;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public interface IMessageHandler
{
    Task<IQueueCallbackResult> HandleAsync(DurableTaskClient durableTaskClient, JobContextDto jobContextDto, CancellationToken cancellationToken);
}