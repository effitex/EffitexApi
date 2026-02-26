namespace EffiTex.Api.Storage;

public interface IBlobStorageService
{
    Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string blobPath, CancellationToken ct = default);
    Task DeleteAsync(string blobPath, CancellationToken ct = default);
    Task<bool> ExistsAsync(string blobPath, CancellationToken ct = default);
}
