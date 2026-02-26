namespace EffiTex.Api.Data.Entities;

public class DocumentEntity
{
    public Guid Id { get; set; }
    public string BlobPath { get; set; }
    public string FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public ICollection<JobEntity> Jobs { get; set; }
}
