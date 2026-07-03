using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Activities;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

public class ProcessJobOrchestrator
{
    [Function(nameof(ProcessJobOrchestrator))]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<ProcessJobOrchestrator>();
        var job = context.GetInput<ProcessJobMessage>()!;

        logger.LogInformation("Processing job {JobId} for UkPrn {UkPrn} (InstanceId: {InstanceId}).", job.JobId, job.KeyValuePairs.Ukprn, context.InstanceId);

        var fileRef = new IlrFileReference
        {
            Container = job.KeyValuePairs.Container,
            Filename = job.KeyValuePairs.Filename
        };

        var learners = await context.CallActivityAsync<List<LearnerSummary>>(
            nameof(DownloadAndParseIlrActivity), fileRef);

        logger.LogInformation("Found {LearnerCount} learners in job {JobId} (InstanceId: {InstanceId}).", learners.Count, job.JobId, context.InstanceId);

        var subOrchestrations = learners.Select(learner =>
            context.CallSubOrchestratorAsync<FundingRuleValidationResultMessage>(
                nameof(ValidateLearnerOrchestrator),
                new ValidateLearnerMessage
                {
                    JobId = job.JobId,
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

        var jobComplete = new JobCompleteMessage
        {
            JobId = job.JobId,
            Ukprn = job.KeyValuePairs.Ukprn,
            TotalLearners = results.Length,
            ValidCount = results.Count(r => r.IsValid),
            InvalidCount = results.Count(r => !r.IsValid)
        };

        await context.CallActivityAsync(nameof(SendJobCompleteActivity), jobComplete);

        logger.LogInformation("Job {JobId} complete. Valid: {ValidCount}, Invalid: {InvalidCount} (InstanceId: {InstanceId}).",
            job.JobId, jobComplete.ValidCount, jobComplete.InvalidCount, context.InstanceId);
    }
}
