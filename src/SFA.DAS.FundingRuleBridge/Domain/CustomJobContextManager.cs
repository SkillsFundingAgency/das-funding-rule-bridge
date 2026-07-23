using ESFA.DC.Auditing.Interface;
using ESFA.DC.JobContext.Interface;
using ESFA.DC.JobContextManager;
using ESFA.DC.JobContextManager.Interface;
using ESFA.DC.JobContextManager.Model;
using ESFA.DC.JobStatus.Interface;
using ESFA.DC.Logging.Interfaces;
using ESFA.DC.Queueing.Interface;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public class CustomJobContextManager : JobContextManager<JobContextMessage>, IMessageHandler
{
    public CustomJobContextManager(
        ITopicPublishService<JobContextDto> topicPublishService,
        IMapper<JobContextMessage, JobContextMessage> mapper,
        IQueuePublishService<JobStatusDto> jobStatusDtoQueuePublishService,
        IQueuePublishService<AuditingDto> auditingDtoQueuePublishService,
        ILogger logger,
        IMessageHandler<JobContextMessage> messageHandler)
        : base(new NullTopicSubscriptionService(),
            topicPublishService,
            mapper,
            jobStatusDtoQueuePublishService,
            auditingDtoQueuePublishService,
            logger,
            messageHandler) { }

    public CustomJobContextManager(
        ITopicPublishService<JobContextDto> topicPublishService,
        IMapper<JobContextMessage, JobContextMessage> mapper,
        IQueuePublishService<JobStatusDto> jobStatusDtoQueuePublishService,
        IQueuePublishService<AuditingDto> auditingDtoQueuePublishService,
        ILogger logger,
        IMessageHandler<JobContextMessage> messageHandler,
        IMapper<JobContextDto, JobContextMessage> jobContextDtoToMessageMapper,
        IJobContextMessageMetadataService jobContextMessageMetadataService) 
        : base(new NullTopicSubscriptionService(),
            topicPublishService,
            mapper,
            jobStatusDtoQueuePublishService,
            auditingDtoQueuePublishService,
            logger,
            messageHandler,
            jobContextDtoToMessageMapper,
            jobContextMessageMetadataService) { }

    public async Task<IQueueCallbackResult> HandleAsync(JobContextDto jobContextDto, CancellationToken cancellationToken)
    {
        return await Callback(jobContextDto, new Dictionary<string, string>(), cancellationToken);
    }
}