using System.Text.Json;
using EffiTex.Api.Data;
using EffiTex.Api.Storage;
using EffiTex.Engine.Models.Inspect;

namespace EffiTex.Api.Jobs;

public class InspectJob
{
    private readonly IDocumentRepository _repo;
    private readonly IBlobStorageService _blobs;
    private readonly IInspectRunner _runner;

    public InspectJob(IDocumentRepository repo, IBlobStorageService blobs, IInspectRunner runner)
    {
        _repo = repo;
        _blobs = blobs;
        _runner = runner;
    }

    public async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _repo.GetJobAsync(jobId, ct);
        if (job is null)
            return;

        await _repo.UpdateJobStatusAsync(jobId, "processing", null, null, ct);

        try
        {
            using var pdfStream = await _blobs.DownloadSourceAsync(job.DocumentId.ToString(), ct);

            var result = _runner.Inspect(pdfStream);

            var json = JsonSerializer.Serialize(result);
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

            var resultId = jobId.ToString();
            await _blobs.UploadInspectResultAsync(resultId, jsonStream, ct);

            await _repo.UpdateJobStatusAsync(jobId, "complete", resultId, null, ct);
        }
        catch (Exception ex)
        {
            await _repo.UpdateJobStatusAsync(jobId, "failed", null, ex.Message, ct);
            throw;
        }
    }
}
