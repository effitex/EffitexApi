using EffiTex.Api.Data.Entities;

namespace EffiTex.Api.Data;

public interface IDocumentRepository
{
    Task<DocumentEntity> CreateDocumentAsync(DocumentEntity document, CancellationToken ct = default);
    Task<DocumentEntity> GetDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<JobEntity> CreateJobAsync(JobEntity job, CancellationToken ct = default);
    Task<JobEntity> GetJobAsync(Guid jobId, CancellationToken ct = default);
    Task UpdateJobStatusAsync(Guid jobId, string status, string resultBlobPath = null, string error = null, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentEntity>> GetExpiredDocumentsAsync(DateTimeOffset cutoff, CancellationToken ct = default);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default);
}
