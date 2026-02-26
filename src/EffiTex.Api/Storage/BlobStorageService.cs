using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace EffiTex.Api.Storage;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;

    public BlobStorageService(BlobServiceClient client, string containerName)
    {
        _container = client.GetBlobContainerClient(containerName);
        _container.CreateIfNotExists();
    }

    public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        await blob.UploadAsync(content, overwrite: true, ct);
    }

    public async Task<Stream> DownloadAsync(string blobPath, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        var response = await blob.DownloadAsync(ct);
        return response.Value.Content;
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.None, null, ct);
    }

    public async Task<bool> ExistsAsync(string blobPath, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        var response = await blob.ExistsAsync(ct);
        return response.Value;
    }
}
