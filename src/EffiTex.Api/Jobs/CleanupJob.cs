using EffiTex.Api.Data;
using EffiTex.Api.Storage;
using Microsoft.Extensions.Logging;

namespace EffiTex.Api.Jobs;

public class CleanupJob
{
    private readonly IDocumentRepository _repo;
    private readonly IBlobStorageService _blobs;
    private readonly ILogger<CleanupJob> _logger;

    public CleanupJob(IDocumentRepository repo, IBlobStorageService blobs, ILogger<CleanupJob> logger)
    {
        _repo = repo;
        _blobs = blobs;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow;
        var expired = await _repo.GetExpiredDocumentsAsync(cutoff, ct);

        foreach (var doc in expired)
        {
            try
            {
                foreach (var job in doc.Jobs)
                {
                    if (job.ResultBlobPath is not null)
                    {
                        if (job.JobType == "inspect")
                            await _blobs.DeleteInspectResultAsync(job.ResultBlobPath, ct);
                        else
                            await _blobs.DeleteExecuteResultAsync(job.ResultBlobPath, ct);
                    }
                }

                await _blobs.DeleteSourceAsync(doc.Id.ToString(), ct);
                await _repo.DeleteDocumentAsync(doc.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean up document {DocumentId}", doc.Id);
            }
        }
    }
}
