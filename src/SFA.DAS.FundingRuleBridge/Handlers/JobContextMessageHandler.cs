using Castle.Core.Logging;
using ESFA.DC.JobContextManager.Interface;
using ESFA.DC.JobContextManager.Model;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;
using System;
using System.Collections.Generic;
using System.Text;

namespace SFA.DAS.FundingRuleBridge.Jobs.Handlers
{
    public class JobContextMessageHandler: IMessageHandler<JobContextMessage>
    {
        private ILogger<JobContextMessageHandler> logger;
        private readonly DurableTaskClient durableClient;

        public JobContextMessageHandler(ILogger<JobContextMessageHandler> logger, DurableTaskClient durableClient) 
        {
            this.logger = logger;
            this.durableClient = durableClient;
        }

        public async Task<bool> HandleAsync(JobContextMessage message, CancellationToken cancellationToken) 
        {
            logger.LogDebug($"Handling job context message for job id: {message.JobId}");
            var instanceId = $"as-valiation-{message.JobId}";
            var existingInstance = await durableClient.GetInstanceAsync(instanceId, cancellationToken);

            if (existingInstance != null && 
                existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
            {
                //TODO: add telemetry to track the number of completed AS validation orchestrations.
                logger.LogInformation($"AS validation for job id {message.JobId} has already completed.");
                return true;
            }

            if (existingInstance != null &&
                (existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Suspended ||
                existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Pending))
            {
                logger.LogInformation($"Previous AS validation job  '{message.JobId}' is still running, waiting for that instance to complete");
            }

            if (existingInstance != null && 
                (existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated))
            {
                logger.LogWarning($"Previous AS validation job  '{message.JobId}' appears to have failed.  Re-running the AS Validation job.");
                //TODO: add telemetry to track the number of failed AS validation orchestrations.
                existingInstance = null;
            }

            if (existingInstance == null)
            {
                logger.LogDebug($"Starting AS validation orchestration for job id: {message.JobId}");
                await durableClient.ScheduleNewOrchestrationInstanceAsync(
                    nameof(ProcessJobOrchestrator), message, new StartOrchestrationOptions(instanceId), cancellationToken);
                logger.LogInformation($"AS validation orchestration started for job id: {message.JobId} with instance id: {instanceId}");
            }

            existingInstance = await durableClient.WaitForInstanceCompletionAsync(instanceId, true, cancellationToken);
    
            cancellationToken.ThrowIfCancellationRequested();


            if (existingInstance.RuntimeStatus != OrchestrationRuntimeStatus.Completed)
            {
                //TODO: add telemetry to track the number of failed AS validation orchestrations.
                logger.LogError($"AS validation orchestration did not complete successfully. Job id: {message.JobId}, status: {existingInstance.RuntimeStatus}");
                return false;
            }
            //TODO: add telemetry to track the number of successful AS validation orchestrations.
            logger.LogInformation($"AS validation orchestration completed successfully for job id: {message.JobId}");
            return true;
        }
    }
}
