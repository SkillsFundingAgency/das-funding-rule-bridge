using System.Text.Json;
using System.Xml.Serialization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ESFA.DC.ILR.IO.Model.Validation;
using ESFA.DC.ILR.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Infrastructure;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public partial class WriteJobsResultsActivity(IIlrBlobStorageClient blobServiceClient, XmlSerializer xmlSerializer, ILogger<WriteJobsResultsActivity> logger)
{
    private const string ValidationErrorsFilename = "ASValidationErrors.json";
    
    [Function(nameof(WriteJobsResultsActivity))]
    public async Task Run([ActivityTrigger] WriteJobResultsRequest request, FunctionContext context)
    {
        var client = blobServiceClient.GetBlobContainerClient(request.Job.Container);
        await WriteValidationErrorsAsync(client, request.Job, request.ValidationErrors, context.CancellationToken);
        await AppendInvalidLearnerRefsAsync(client, request.Job, request.InvalidLearnerRefs, context.CancellationToken);
        await WriteValidIlrAsync(client, request.Job, request.InvalidLearnerRefs, context.CancellationToken);
    }

    private async Task AppendInvalidLearnerRefsAsync(BlobContainerClient client, JobInfo jobInfo, List<string> invalidLearnerRefs, CancellationToken cancellationToken)
    {
        if (invalidLearnerRefs is not { Count: > 0 })
        {
            return;
        }

        List<string> learnerRefs;
        var blobClient = client.GetBlobClient(jobInfo.InvalidLearnerRefsFilename);
        await using (var stream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken))
        {
            var reader = new StreamReader(stream);
            learnerRefs = JsonSerializer.Deserialize<List<string>>(await reader.ReadToEndAsync(cancellationToken)) ?? [];
        }
        
        learnerRefs.AddRange(invalidLearnerRefs);
        learnerRefs = learnerRefs.Distinct().ToList();
        
        // uploading with the original filename overwrites
        await using var sw = new StringWriter();
        var payload = BinaryData.FromString(JsonSerializer.Serialize(learnerRefs));
        await client.UploadBlobAsync(jobInfo.ValidIlrXmlFilename, payload, cancellationToken);
    }

    private async Task WriteValidIlrAsync(BlobContainerClient client, JobInfo jobInfo, List<string> invalidLearnerRefs, CancellationToken cancellationToken)
    {
        if (invalidLearnerRefs is not { Count: > 0 })
        {
            return;
        }
            
        var ids = invalidLearnerRefs.ToHashSet();
        var blobClient = client.GetBlobClient(jobInfo.ValidIlrXmlFilename);

        Message message;
        await using (var stream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken))
        {
            message = (Message)xmlSerializer.Deserialize(stream)!;
        }
        
        // filter out the learners who failed validation
        message.Learner = message.Learner.Where(x => !ids.Contains(x.LearnRefNumber)).ToArray();
        
        // TODO: do we have to do anything with message.SourceFiles?
        
        // uploading with the original filename overwrites
        await using var sw = new StringWriter();
        xmlSerializer.Serialize(sw, message);
        await client.UploadBlobAsync(jobInfo.ValidIlrXmlFilename, BinaryData.FromString(sw.ToString()), cancellationToken);
    }

    private async Task WriteValidationErrorsAsync(BlobContainerClient client, JobInfo jobInfo, List<ValidationError> validationErrors, CancellationToken cancellationToken = default)
    {
        if (validationErrors is not { Count: > 0 })
        {
            return;
        }
        
        var filename = jobInfo.GetJobPath(ValidationErrorsFilename);
        var payload = BinaryData.FromString(JsonSerializer.Serialize(validationErrors));
        await client.UploadBlobAsync(filename, payload, cancellationToken);
        LogValidationErrorsWritten(validationErrors.Count, jobInfo.Container, filename);
    }

    [LoggerMessage(LogLevel.Information, "Wrote {ValidationErrorCount} validation error records to {ContainerName}/{ValidationErrorsFilename}")]
    partial void LogValidationErrorsWritten(int validationErrorCount, string containerName, string validationErrorsFilename);
}