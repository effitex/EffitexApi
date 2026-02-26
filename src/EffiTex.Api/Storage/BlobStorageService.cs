using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace EffiTex.Api.Storage;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _uploadClient;
    private readonly BlobContainerClient _inspectClient;
    private readonly BlobContainerClient _executeClient;

    public BlobStorageService(BlobServiceClient client, string containerName)
        : this(client, containerName, containerName, containerName) { }

    public BlobStorageService(BlobServiceClient client, string uploadContainer, string inspectContainer, string executeContainer)
    {
        _uploadClient = client.GetBlobContainerClient(uploadContainer);
        _inspectClient = client.GetBlobContainerClient(inspectContainer);
        _executeClient = client.GetBlobContainerClient(executeContainer);
        _uploadClient.CreateIfNotExists();
        _inspectClient.CreateIfNotExists();
        _executeClient.CreateIfNotExists();
    }

    public async Task UploadSourceAsync(string documentId, Stream content, CancellationToken ct = default)
    {
        var blob = _uploadClient.GetBlobClient($"{documentId}.pdf");
        await blob.UploadAsync(content, overwrite: true, ct);
    }

    public async Task<Stream> DownloadSourceAsync(string documentId, CancellationToken ct = default)
    {
        var blob = _uploadClient.GetBlobClient($"{documentId}.pdf");
        var response = await blob.DownloadAsync(ct);
        return response.Value.Content;
    }

    public async Task DeleteSourceAsync(string documentId, CancellationToken ct = default)
    {
        var blob = _uploadClient.GetBlobClient($"{documentId}.pdf");
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.None, null, ct);
    }

    public async Task UploadInspectResultAsync(string jobId, Stream content, CancellationToken ct = default)
    {
        var blob = _inspectClient.GetBlobClient($"{jobId}.json");
        await blob.UploadAsync(content, overwrite: true, ct);
    }

    public async Task<Stream> DownloadInspectResultAsync(string jobId, CancellationToken ct = default)
    {
        var blob = _inspectClient.GetBlobClient($"{jobId}.json");
        var response = await blob.DownloadAsync(ct);
        return response.Value.Content;
    }

    public async Task DeleteInspectResultAsync(string jobId, CancellationToken ct = default)
    {
        var blob = _inspectClient.GetBlobClient($"{jobId}.json");
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.None, null, ct);
    }

    public async Task UploadExecuteResultAsync(string jobId, Stream content, CancellationToken ct = default)
    {
        var blob = _executeClient.GetBlobClient($"{jobId}.pdf");
        await blob.UploadAsync(content, overwrite: true, ct);
    }

    public async Task<Stream> DownloadExecuteResultAsync(string jobId, CancellationToken ct = default)
    {
        var blob = _executeClient.GetBlobClient($"{jobId}.pdf");
        var response = await blob.DownloadAsync(ct);
        return response.Value.Content;
    }

    public async Task DeleteExecuteResultAsync(string jobId, CancellationToken ct = default)
    {
        var blob = _executeClient.GetBlobClient($"{jobId}.pdf");
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.None, null, ct);
    }
}
