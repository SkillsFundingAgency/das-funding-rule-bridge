using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace SFA.DAS.FundingRuleBridge.Jobs.Endpoints;

public class TestEndpoint(ILogger<TestEndpoint> logger)
{
    [Function(nameof(TestEndpoint))]
    public async Task Test([TimerTrigger("0 0 29 2 1")] TimerInfo timer, [DurableClient] DurableTaskClient durableClient)
    {
        var parameters = new Dictionary<string, string>
        {
            { "TestId", Guid.NewGuid().ToString() }
        };

        using var _ = logger.BeginScope(parameters);
        logger.LogInformation("Scheduling orchestrator");

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(nameof(TestOrchestrator));
        
        using (logger.BeginScope(new Dictionary<string, string> { { "InstanceId", instanceId }, { "TestId", Guid.NewGuid().ToString() } }))
        {
            logger.LogInformation("Orchestration scheduled");
        }
    }
}
public class TestOrchestrator
{
    [Function(nameof(TestOrchestrator))]
    public static async Task<bool> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<TestOrchestrator>();
        var parameters = new Dictionary<string, string>
        {
            { "TestId", Guid.NewGuid().ToString() }
        };
        
        using var _ = logger.BeginScope(parameters);
        logger.LogInformation("Executing orchestrator");
        
        using (logger.BeginScope(new Dictionary<string, string> { { "TestId", Guid.NewGuid().ToString() } }))
        {
            logger.LogInformation("Orchestration execution completed");
        }
        return true;
    }
}