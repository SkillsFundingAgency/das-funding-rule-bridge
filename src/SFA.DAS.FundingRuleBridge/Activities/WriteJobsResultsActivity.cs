using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Infrastructure;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public partial class WriteJobsResultsActivity(IIlrBlobStorageClient blobServiceClient, ILogger<WriteJobsResultsActivity> logger)
{
    private const string InvalidLearnersFilename = "ASInvalidLearnRefNumbers.json";
    private const string ValidationErrorsFilename = "ASValidationErrors.json";
    
    [Function(nameof(WriteJobsResultsActivity))]
    public async Task Run([ActivityTrigger] WriteJobResultsRequest request, FunctionContext context)
    {
        var client = blobServiceClient.GetBlobContainerClient(request.ContainerName);
        var learnerRefs = request.ValidationErrors
            .DistinctBy(x => x.LearnerReferenceNumber)
            .ToList();
        
        await client.UploadBlobAsync(InvalidLearnersFilename, BinaryData.FromString(JsonSerializer.Serialize(learnerRefs)), context.CancellationToken);
        LogSummaryFileWritten(request.JobId, learnerRefs.Count, request.ContainerName, InvalidLearnersFilename);
        
        await client.UploadBlobAsync(ValidationErrorsFilename, BinaryData.FromString(JsonSerializer.Serialize(request.ValidationErrors)), context.CancellationToken);
        LogValidationErrorsWritten(request.JobId, learnerRefs.Count, request.ContainerName, ValidationErrorsFilename);
    }

    [LoggerMessage(LogLevel.Information, "{JobId}: Wrote {FailedLearnerRefCount} unique learner refs to {ContainerName}/{InvalidLearnersFilename}")]
    partial void LogSummaryFileWritten(long jobId, int failedLearnerRefCount, string containerName, string invalidLearnersFilename);

    [LoggerMessage(LogLevel.Information, "{JobId}: Wrote {ValidationErrorCount} validation error records to {ContainerName}/{ValidationErrorsFilename}")]
    partial void LogValidationErrorsWritten(long jobId, int validationErrorCount, string containerName, string validationErrorsFilename);
}