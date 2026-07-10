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
            var fileRef = new IlrFileReference
            {
                Container = job.KeyValuePairs.Container,
                Filename = job.KeyValuePairs.Filename
            };

            var learners = await context.CallActivityAsync<List<LearnerSummary>>(nameof(DownloadAndParseIlrActivity), fileRef);
            var results = await RunValidation(context, job, learners, logger);
            await WriteJobFiles(context, job, results);
            await CompleteJob(context, job, results, logger);    
        }
    }

    private static async Task<ValidationSummary[]> RunValidation(TaskOrchestrationContext context, ProcessJobMessage job, List<LearnerSummary> learners, ILogger logger)
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

        // all learners are validated regardless of individual pass/fail outcomes;
        // only an infrastructure exception will cause Task.WhenAll to throw
        var results = await Task.WhenAll(subOrchestrations);
        logger.LogInformation("Fan in complete");
        
        return results;
    }

    private static async Task WriteJobFiles(TaskOrchestrationContext context, ProcessJobMessage job, ValidationSummary[] results)
    {
        if (results.All(x => x.IsValid))
        {
            // nothing to write
            return;
        }
        
        var writeSummaryRequest = new WriteJobResultsRequest()
        {
            JobId = job.JobId,
            ContainerName = job.KeyValuePairs.Container,
            ValidationErrors = results.SelectMany(x => x.ValidationErrors).ToList()
        };
        await context.CallActivityAsync(nameof(WriteJobsResultsActivity), writeSummaryRequest);
    }

    private static async Task CompleteJob(TaskOrchestrationContext context, ProcessJobMessage job, ValidationSummary[] results, ILogger logger)
    {
        var message = new JobCompleteMessage
        {
            JobId = job.JobId,
            Ukprn = job.KeyValuePairs.Ukprn,
            TotalLearners = results.Length,
            ValidCount = results.Count(x => x.IsValid),
            InvalidCount = results.Count(x => !x.IsValid)
        };

        await context.CallActivityAsync(nameof(SendJobCompleteActivity), message);
        logger.LogInformation("Job complete (valid: {ValidCount}, invalid: {InvalidCount})", message.ValidCount, message.InvalidCount);
    }
}
