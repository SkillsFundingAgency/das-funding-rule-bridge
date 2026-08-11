using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ESFA.DC.JobContext.Interface;
using ESFA.DC.JobContextManager.Model;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

namespace SFA.DAS.FundingRuleBridge.Jobs.Handlers;

[SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging")]
public class JobContextMessageHandler(ILogger<JobContextMessageHandler> logger): IJobContextMessageHandler
{
    public DurableTaskClient DurableClient { get; set; }
    
    public async Task<bool> HandleAsync(JobContextMessage message, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { { "JobHandlerId", Guid.NewGuid() }, { "JobId", message.JobId } });
        
        logger.LogInformation("Received JobContextMessage");
        var instanceId = $"as-validation-{message.JobId}";
        var existingInstance = await DurableClient.GetInstanceAsync(instanceId, cancellationToken);
            
        if (existingInstance is { RuntimeStatus: OrchestrationRuntimeStatus.Completed })
        {
            existingInstance = null;
            instanceId = $"{instanceId}-{Guid.NewGuid()}";
            logger.LogInformation("Previous Job has completed, generating unique id '{UniqueInstanceId}' for subsequent job", instanceId);
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
            if (!TryGetJobInfo(message, logger, out var jobInfo))
            {
                return false;
            }
            
            logger.LogInformation("Starting AS validation orchestration");
            await DurableClient.ScheduleNewOrchestrationInstanceAsync(nameof(ProcessJobOrchestrator), jobInfo, new StartOrchestrationOptions(instanceId), cancellationToken);
            logger.LogInformation("AS validation orchestration started with instance id: {InstanceId}", instanceId);
        }

        existingInstance = await DurableClient.WaitForInstanceCompletionAsync(instanceId, true, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        
        if (existingInstance.RuntimeStatus != OrchestrationRuntimeStatus.Completed)
        {
            logger.LogError("Job did not complete successfully, status: {FinalStatus}", existingInstance.RuntimeStatus);
            return false;
        }

        if (TryGetJobResult(existingInstance, out var jobResult))
        {
            logger.LogInformation("Job completed with result: {JobResult}", jobResult.Value ? "Success" : "Failure");
            return jobResult.Value;
        }

        logger.LogError("Job completed successfully but did not contain a JobResult");
        return false;
    }

    private static bool TryGetJobResult(OrchestrationMetadata existingInstance, [NotNullWhen(true)]out bool? jobResult)
    {
        jobResult = null;
        if (existingInstance.SerializedOutput is null)
        {
            return false;
        }
        
        try
        {
            jobResult = JsonSerializer.Deserialize<bool?>(existingInstance.SerializedOutput);
            return jobResult.HasValue;
        }
        catch
        {
            return false;
        }
    }
    
    private static bool TryGetJobInfo(JobContextMessage jobContextMessage, ILogger logger, [NotNullWhen(true)] out JobInfo? jobInfo)
    {
        jobInfo = null;

        if (!jobContextMessage.KeyValuePairs.TryGetValue(JobContextMessageKey.Container, out var container))
        {
            logger.LogCritical("JobContextMessage does not contain the Container value");
            return false;
        }
        
        if (!jobContextMessage.KeyValuePairs.TryGetValue(JobContextMessageKey.UkPrn, out var ukprn))
        {
            logger.LogCritical("JobContextMessage does not contain the Ukprn value");
            return false;
        }
        
        if (!jobContextMessage.KeyValuePairs.TryGetValue(JobContextMessageKey.Filename, out var filename))
        {
            logger.LogCritical("JobContextMessage does not contain the Filename value");
            return false;
        }
        
        jobInfo = new JobInfo
        {
            JobId = jobContextMessage.JobId,
            Ukprn = (string)ukprn,
            Container = (string)container,
            ValidIlrXmlFilename = (string)filename,
        };
        
        return true;
    }
}