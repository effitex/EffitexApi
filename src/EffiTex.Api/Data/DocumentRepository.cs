using EffiTex.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EffiTex.Api.Data;

public class DocumentRepository : IDocumentRepository
{
    private readonly EffiTexDbContext _ctx;

    public DocumentRepository(EffiTexDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<DocumentEntity> CreateDocumentAsync(DocumentEntity document, CancellationToken ct = default)
    {
        _ctx.Documents.Add(document);
        await _ctx.SaveChangesAsync(ct);
        return document;
    }

    public async Task<DocumentEntity> GetDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        return await _ctx.Documents.FindAsync(new object[] { documentId }, ct);
    }

    public async Task<JobEntity> CreateJobAsync(JobEntity job, CancellationToken ct = default)
    {
        _ctx.Jobs.Add(job);
        await _ctx.SaveChangesAsync(ct);
        return job;
    }

    public async Task<JobEntity> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        return await _ctx.Jobs.FindAsync(new object[] { jobId }, ct);
    }

    public async Task UpdateJobStatusAsync(Guid jobId, string status, string resultBlobPath = null, string error = null, CancellationToken ct = default)
    {
        var job = await _ctx.Jobs.FindAsync(new object[] { jobId }, ct);
        if (job == null) return;

        job.Status = status;

        if (resultBlobPath != null)
            job.ResultBlobPath = resultBlobPath;

        if (error != null)
            job.Error = error;

        if (status == "complete" || status == "failed")
            job.CompletedAt = DateTimeOffset.UtcNow;

        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentEntity>> GetExpiredDocumentsAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        return await _ctx.Documents
            .Include(d => d.Jobs)
            .Where(d => d.ExpiresAt < cutoff)
            .ToListAsync(ct);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var doc = await _ctx.Documents.FindAsync(new object[] { documentId }, ct);
        if (doc == null) return;

        _ctx.Documents.Remove(doc);
        await _ctx.SaveChangesAsync(ct);
    }
}
