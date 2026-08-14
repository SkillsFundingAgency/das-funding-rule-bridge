using ESFA.DC.JobContext.Interface;
using ESFA.DC.Queueing.Interface;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public class NullTopicSubscriptionService : ITopicSubscriptionService<JobContextDto>
{
    public Task Subscribe(Func<JobContextDto, IDictionary<string, object>, CancellationToken, Task<IQueueCallbackResult>> callback, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync()
    {
        return Task.CompletedTask;
    }
}