using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Activities;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

public class ProcessJobOrchestrator
{
    [Function(nameof(ProcessJobOrchestrator))]
    public static async Task RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<ProcessJobOrchestrator>();
        var job = context.GetInput<ProcessJobMessage>()!;

        var properties = new Dictionary<string, object>
        {
            { "CorrelationId", context.InstanceId },
            { "JobId", job.JobId },
        };
        
        using (logger.BeginScope(properties))
        {
            logger.LogInformation("Processing job for UkPrn {UkPrn}", job.KeyValuePairs.Ukprn);
            var jobInfo = new JobInfo
            {
                JobId = job.JobId,
                Ukprn = job.KeyValuePairs.Ukprn,
                Container = job.KeyValuePairs.Container,
                ValidIlrXmlFilename = job.KeyValuePairs.Filename,
                InvalidLearnerRefsFilename = job.KeyValuePairs.InvalidLearnRefNumbers,
            };
            
            try
            {
                var learners = await context.CallActivityAsync<List<LearnerSummary>>(nameof(DownloadAndParseIlrActivity), jobInfo);
                JobSummary jobSummary = await RunValidationAsync(context, job, learners, logger);

                if (jobSummary.JobFailure)
                {
                    logger.LogCritical("Job failed");
                    await SendJobFailedMessageAsync(context, job, logger);    
                }
                else
                {
                    await WriteJobFilesAsync(context, jobInfo, jobSummary);
                    await CompleteJob(context, jobInfo, jobSummary, logger);
                } 
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Job failed");
                await SendJobFailedMessageAsync(context, job, logger);
            }
        }
    }

    private static async Task<JobSummary> RunValidationAsync(TaskOrchestrationContext context, ProcessJobMessage job, List<LearnerSummary> learners, ILogger logger)
    {
        logger.LogInformation("Fan out started");
        var subOrchestrations = learners.Select(learner =>
            context.CallSubOrchestratorAsync<ValidationSummary>(
                nameof(ValidateLearnerOrchestrator),
                new ValidateLearnerMessage
                {
                    JobId = job.JobId,
                    CorrelationId = context.InstanceId,
                    Ukprn = job.KeyValuePairs.Ukprn,
                    Uln = learner.LearnRefNumber,
                    DateOfBirth = learner.DateOfBirth,
                    Courses = learner.Courses,
                    Container = job.KeyValuePairs.Container,
                    Filename = job.KeyValuePairs.Filename
                }));

        var results = await Task.WhenAll(subOrchestrations);
        logger.LogInformation("Fan in complete");

        return results.ToJobSummary();
    }

    private static async Task WriteJobFilesAsync(TaskOrchestrationContext context, JobInfo jobInfo, JobSummary jobSummary)
    {
        if (jobSummary.JobFailure || jobSummary.InvalidLearnerRefs is not { Count: > 0 })
        {
            // nothing to write
            return;
        }
        
        var writeSummaryRequest = new WriteJobResultsRequest
        {
            Job = jobInfo,
            ValidationErrors = jobSummary.Items.SelectMany(x => x.ValidationErrors).ToList(),
            InvalidLearnerRefs = jobSummary.InvalidLearnerRefs,
            RuleDescriptions = jobSummary.RuleDescriptions,
        };
        await context.CallActivityAsync(nameof(WriteJobsResultsActivity), writeSummaryRequest);
    }

    private static async Task CompleteJob(TaskOrchestrationContext context, JobInfo jobInfo, JobSummary jobSummary, ILogger logger)
    {
        // TODO: this probably isn't the format of the message to return
        var message = new JobCompleteMessage
        {
            JobId = jobInfo.JobId,
            Ukprn = jobInfo.Ukprn,
            TotalLearners = jobSummary.Items.Count,
            ValidCount = jobSummary.Items.Count(x => x.Status == ValidationStatus.Passed),
            InvalidCount = jobSummary.InvalidLearnerRefs.Count,
        };

        await context.CallActivityAsync(nameof(SendJobCompleteActivity), message);
        logger.LogInformation("Job complete (valid: {ValidCount}, invalid: {InvalidCount})", message.ValidCount, message.InvalidCount);
    }
    
    private static async Task SendJobFailedMessageAsync(TaskOrchestrationContext context, ProcessJobMessage job, ILogger logger)
    {
        var message = new JobCompleteMessage
        {
            JobId = job.JobId,
            Ukprn = job.KeyValuePairs.Ukprn,
        };

        await context.CallActivityAsync(nameof(SendJobCompleteActivity), message);
        logger.LogInformation("Sent job failed message");
    }
}