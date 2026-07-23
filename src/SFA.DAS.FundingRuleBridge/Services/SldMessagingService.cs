using ESFA.DC.JobContextManager.Interface;
using ESFA.DC.JobContextManager.Model;
using Microsoft.Extensions.Hosting;

namespace SFA.DAS.FundingRuleBridge.Jobs.Services;

public class SldMessagingService(IJobContextManager<JobContextMessage> jobContextManager): IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        jobContextManager.OpenAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return jobContextManager.CloseAsync();
    }
}