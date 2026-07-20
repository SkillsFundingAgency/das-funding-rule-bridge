using System.Text.Json;
using System.Xml.Serialization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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
    private const string InvalidLearnersFilename = "ASInvalidLearnRefNumbers.json";
    private const string ErrorLookupsFilename = "ASValidationErrorLookups.json";
    
    [Function(nameof(WriteJobsResultsActivity))]
    public async Task Run([ActivityTrigger] WriteJobResultsRequest request, FunctionContext context)
    {
        if (request.InvalidLearnerRefs is not { Count: > 0 })
        {
            return;
        }
        
        var client = blobServiceClient.GetBlobContainerClient(request.Job.Container);
        await WriteJsonFile(client, request.Job.GetJobPath(ValidationErrorsFilename), request.ValidationErrors, context.CancellationToken);
        await WriteJsonFile(client, request.Job.GetJobPath(InvalidLearnersFilename), request.InvalidLearnerRefs, context.CancellationToken);
        await WriteJsonFile(client, request.Job.GetJobPath(ErrorLookupsFilename), request.RuleDescriptions, context.CancellationToken);
        await UpdateIlrAsync(client, request.Job, request.InvalidLearnerRefs, context.CancellationToken);
    }

    private async Task UpdateIlrAsync(BlobContainerClient client, JobInfo jobInfo, List<string> invalidLearnerRefs, CancellationToken cancellationToken)
    {
        var ids = invalidLearnerRefs.ToHashSet();
        var blobClient = client.GetBlobClient(jobInfo.ValidIlrXmlFilename);

        Message message;
        await using (var stream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken))
        {
            message = (Message)xmlSerializer.Deserialize(stream)!;
        }
        
        // filter out the learners who failed validation
        message.Learner = message.Learner.ExceptBy(ids, x => x.LearnRefNumber).ToArray();
        
        await using var sw = new StringWriter();
        xmlSerializer.Serialize(sw, message);
        
        await blobClient.UploadAsync(BinaryData.FromString(sw.ToString()), overwrite: true, cancellationToken);
        LogFileUpload(jobInfo.ValidIlrXmlFilename);
    }

    private async Task WriteJsonFile<T>(BlobContainerClient client, string filename, T content, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(content);
        var payload = BinaryData.FromString(json);
        await client.UploadBlobAsync(filename, payload, cancellationToken);
        LogFileUpload(filename);
    }

    [LoggerMessage(LogLevel.Information, "Wrote {Filename}")]
    partial void LogFileUpload(string filename);
}