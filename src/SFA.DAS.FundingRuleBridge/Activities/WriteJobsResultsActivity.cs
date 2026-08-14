using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DC.ILR.Model;
using ESFA.DC.Serialization.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Infrastructure;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public partial class WriteJobsResultsActivity(IIlrBlobStorageClient blobServiceClient, IXmlSerializationService serializationService, ILogger<WriteJobsResultsActivity> logger)
{
    private const string ValidationErrorsFilename = "ASValidationErrors.json";
    private const string InvalidLearnersFilename = "ASInvalidLearnRefNumbers.json";
    
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
        await UpdateIlrAsync(client, request.Job, request.InvalidLearnerRefs, context.CancellationToken);
    }

    private async Task UpdateIlrAsync(BlobContainerClient client, JobInfo jobInfo, List<string> invalidLearnerRefs, CancellationToken cancellationToken)
    {
        var ids = invalidLearnerRefs.ToHashSet();
        var blobClient = client.GetBlobClient(jobInfo.ValidIlrXmlFilename);

        Message message;
        await using (var stream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken))
        {
            message = serializationService.Deserialize<Message>(stream);
        }
        
        // filter out the learners who failed validation
        message.Learner = message.Learner.ExceptBy(ids, x => x.LearnRefNumber).ToArray();

        // save the message
        await using var memoryStream = new MemoryStream();
        serializationService.Serialize(message, memoryStream, true);
        var data = BinaryData.FromBytes(memoryStream.ToArray());

        await blobClient.UploadAsync(data, overwrite: true, cancellationToken);
        LogFileUpload(jobInfo.ValidIlrXmlFilename);
    }

    private async Task WriteJsonFile<T>(BlobContainerClient client, string filename, T content, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(content);
        var payload = BinaryData.FromString(json);

        var blobClient = client.GetBlobClient(filename);
        await blobClient.UploadAsync(payload, overwrite: true, cancellationToken);
        LogFileUpload(filename);
    }

    [LoggerMessage(LogLevel.Information, "Wrote {Filename}")]
    partial void LogFileUpload(string filename);
}