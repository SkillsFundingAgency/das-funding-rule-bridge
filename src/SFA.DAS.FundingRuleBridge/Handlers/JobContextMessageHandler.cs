using System.Diagnostics.CodeAnalysis;
using ESFA.DC.JobContextManager.Interface;
using ESFA.DC.JobContextManager.Model;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

namespace SFA.DAS.FundingRuleBridge.Jobs.Handlers;

[SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging")]
public class JobContextMessageHandler(DurableTaskClient durableClient, ILogger<JobContextMessageHandler> logger): IMessageHandler<JobContextMessage>
{
    public async Task<bool> HandleAsync(JobContextMessage message, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { { "JobHandlerId", Guid.NewGuid() }, { "JobId", message.JobId } });
        
        logger.LogInformation("Received JobContextMessage");
        var instanceId = $"as-validation-{message.JobId}";
        var existingInstance = await durableClient.GetInstanceAsync(instanceId, cancellationToken);
            
        if (existingInstance is { RuntimeStatus: OrchestrationRuntimeStatus.Completed })
        {
            logger.LogInformation("Job previously handled and has completed");
            return true;
        }

        if (existingInstance is { RuntimeStatus: OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Suspended or OrchestrationRuntimeStatus.Pending })
        {
            logger.LogInformation("Job already in progress, waiting for that instance to complete");
        }
        
        if (existingInstance is { RuntimeStatus: OrchestrationRuntimeStatus.Failed or OrchestrationRuntimeStatus.Terminated })
        {
            logger.LogWarning("Job has previously failed or been terminated, re-running");
            existingInstance = null;
        }
        
        if (existingInstance == null)
        {
            logger.LogInformation("Starting AS validation orchestration");
            await durableClient.ScheduleNewOrchestrationInstanceAsync(nameof(ProcessJobOrchestrator), message, new StartOrchestrationOptions(instanceId), cancellationToken);
            logger.LogInformation("AS validation orchestration started with instance id: {InstanceId}", instanceId);
        }

        existingInstance = await durableClient.WaitForInstanceCompletionAsync(instanceId, true, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        
        if (existingInstance.RuntimeStatus != OrchestrationRuntimeStatus.Completed)
        {
            logger.LogError("Job did not complete successfully, status: {FinalStatus}", existingInstance.RuntimeStatus);
            return false;
        }
        
        logger.LogInformation("Job completed successfully");
        return true;
    }
}