namespace EffiTex.Api.Storage;

public interface IBlobStorageService
{
    Task UploadSourceAsync(string documentId, Stream content, CancellationToken ct = default);
    Task<Stream> DownloadSourceAsync(string documentId, CancellationToken ct = default);
    Task DeleteSourceAsync(string documentId, CancellationToken ct = default);

    Task UploadInspectResultAsync(string jobId, Stream content, CancellationToken ct = default);
    Task<Stream> DownloadInspectResultAsync(string jobId, CancellationToken ct = default);
    Task DeleteInspectResultAsync(string jobId, CancellationToken ct = default);

    Task UploadExecuteResultAsync(string jobId, Stream content, CancellationToken ct = default);
    Task<Stream> DownloadExecuteResultAsync(string jobId, CancellationToken ct = default);
    Task DeleteExecuteResultAsync(string jobId, CancellationToken ct = default);
}
