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
    public static async Task<bool> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<ProcessJobOrchestrator>();
        var jobInfo = context.GetInput<JobInfo>()!;
        var parameters = new Dictionary<string, string>
        {
            { "JobId", jobInfo.JobId.ToString() },
            { "CorrelationId", context.InstanceId },
        };
        using var scope = logger.BeginScope(parameters);
        
        try
        {
            var learners = await context.CallActivityAsync<List<LearnerSummary>>(nameof(DownloadAndParseIlrActivity), jobInfo);
            var jobSummary = await RunValidationAsync(context, jobInfo, learners, logger);
            if (jobSummary.JobFailure)
            {
                logger.LogCritical("Job failure signalled by downstream activity");
                return false;
            }

            await WriteJobFilesAsync(context, jobInfo, jobSummary);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Job failed with exception");
            return false;
        }
    }

    private static async Task<JobSummary> RunValidationAsync(TaskOrchestrationContext context, JobInfo jobInfo, List<LearnerSummary> learners, ILogger logger)
    {
        logger.LogInformation("Fan out started");
        var subOrchestrations = learners.Select(learner =>
            context.CallSubOrchestratorAsync<ValidationSummary>(
                nameof(ValidateLearnerOrchestrator),
                new ValidateLearnerMessage
                {
                    JobId = jobInfo.JobId,
                    CorrelationId = context.InstanceId,
                    Ukprn = jobInfo.Ukprn,
                    Uln = learner.LearnRefNumber,
                    DateOfBirth = learner.DateOfBirth,
                    Courses = learner.Courses,
                    Container = jobInfo.Container,
                    Filename = jobInfo.ValidIlrXmlFilename
                }));

        var results = await Task.WhenAll(subOrchestrations);
        logger.LogInformation("Fan in complete");
        return results.ToJobSummary();
    }

    private static async Task WriteJobFilesAsync(TaskOrchestrationContext context, JobInfo jobInfo, JobSummary jobSummary)
    {
        if (jobSummary.InvalidLearnerRefs is not { Count: > 0 })
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
}