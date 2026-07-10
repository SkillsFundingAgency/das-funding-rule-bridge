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
            .Select(x => x.LearnerReferenceNumber)
            .ToList();

        var summaryFilename = Path.Combine(request.Path, InvalidLearnersFilename);
        await client.UploadBlobAsync(summaryFilename, BinaryData.FromString(JsonSerializer.Serialize(learnerRefs)), context.CancellationToken);
        LogSummaryFileWritten(learnerRefs.Count, request.ContainerName, summaryFilename);
        
        var detailsFilename = Path.Combine(request.Path, ValidationErrorsFilename);
        await client.UploadBlobAsync(detailsFilename, BinaryData.FromString(JsonSerializer.Serialize(request.ValidationErrors)), context.CancellationToken);
        LogValidationErrorsWritten(learnerRefs.Count, request.ContainerName, detailsFilename);
    }

    [LoggerMessage(LogLevel.Information, "Wrote {FailedLearnerRefCount} unique learner refs to {ContainerName} > {InvalidLearnersFilename}")]
    partial void LogSummaryFileWritten(int failedLearnerRefCount, string containerName, string invalidLearnersFilename);

    [LoggerMessage(LogLevel.Information, "Wrote {ValidationErrorCount} validation error records to {ContainerName} > {ValidationErrorsFilename}")]
    partial void LogValidationErrorsWritten(int validationErrorCount, string containerName, string validationErrorsFilename);
}