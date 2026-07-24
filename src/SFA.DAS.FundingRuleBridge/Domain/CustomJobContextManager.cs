using ESFA.DC.Auditing.Interface;
using ESFA.DC.JobContext.Interface;
using ESFA.DC.JobContextManager;
using ESFA.DC.JobContextManager.Interface;
using ESFA.DC.JobContextManager.Model;
using ESFA.DC.JobStatus.Interface;
using ESFA.DC.Logging.Interfaces;
using ESFA.DC.Queueing.Interface;
using Microsoft.DurableTask.Client;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public class CustomJobContextManager(
    ITopicPublishService<JobContextDto> topicPublishService,
    IMapper<JobContextMessage, JobContextMessage> mapper,
    IQueuePublishService<JobStatusDto> jobStatusDtoQueuePublishService,
    IQueuePublishService<AuditingDto> auditingDtoQueuePublishService,
    ILogger logger,
    IJobContextMessageHandler messageHandler)
    : JobContextManager<JobContextMessage>(new NullTopicSubscriptionService(),
        topicPublishService,
        mapper,
        jobStatusDtoQueuePublishService,
        auditingDtoQueuePublishService,
        logger,
        messageHandler), IMessageHandler
{
    public async Task<IQueueCallbackResult> HandleAsync(DurableTaskClient durableTaskClient, JobContextDto jobContextDto, CancellationToken cancellationToken)
    {
        messageHandler.DurableClient = durableTaskClient;
        return await Callback(jobContextDto, new Dictionary<string, object>(), cancellationToken);
    }
}