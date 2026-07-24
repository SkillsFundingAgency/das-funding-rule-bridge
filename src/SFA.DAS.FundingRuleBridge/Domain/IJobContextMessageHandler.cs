using ESFA.DC.JobContextManager.Interface;
using ESFA.DC.JobContextManager.Model;
using Microsoft.DurableTask.Client;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public interface IJobContextMessageHandler : IMessageHandler<JobContextMessage>
{
    DurableTaskClient DurableClient { get; set; }
}