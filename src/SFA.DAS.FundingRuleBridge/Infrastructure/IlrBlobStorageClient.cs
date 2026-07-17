using Azure.Storage.Blobs;

namespace SFA.DAS.FundingRuleBridge.Jobs.Infrastructure;

public interface IIlrBlobStorageClient
{
    BlobContainerClient GetBlobContainerClient(string containerName);
}

public class IlrBlobStorageClient(string connectionString) : IIlrBlobStorageClient
{
    private readonly BlobServiceClient _inner = new(connectionString);

    public BlobContainerClient GetBlobContainerClient(string containerName)
        => _inner.GetBlobContainerClient(containerName);
}
