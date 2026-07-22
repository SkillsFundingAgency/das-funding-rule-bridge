using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Activities;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

public class ValidateLearnerOrchestrator
{
    private const int ValidationTimeoutInHours = 1;
    
    [Function(nameof(ValidateLearnerOrchestrator))]
    public static async Task<ValidationSummary> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<ValidateLearnerOrchestrator>();
        var input = context.GetInput<ValidateLearnerMessage>()!;
        var request = new ValidationRequestMessage(
            input.Ukprn,
            input.Uln,
            input.Courses,
            input.CorrelationId,
            context.InstanceId
        );

        try
        {
            await context.CallActivityAsync(nameof(SendValidationRequestActivity), request);
            logger.LogInformation("Sent validation request, waiting for result");
        
            var validationResult = await context.WaitForExternalEvent<ValidateLearnerResult>("ValidationComplete", TimeSpan.FromHours(ValidationTimeoutInHours));
            logger.LogInformation("Received validation result");

            return validationResult.ToValidationSummary(request.Uln);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Timed out waiting for validation result, marking as invalid");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occured, marking as invalid");
        }
        
        // system failure
        return new ValidationSummary(request.Uln, ValidationStatus.SystemError, [], []);
    }
}
