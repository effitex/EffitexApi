namespace EffiTex.Api.Data.Entities;

public class JobEntity
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public DocumentEntity Document { get; set; }
    public string JobType { get; set; }
    public string Status { get; set; }
    public string Dsl { get; set; }
    public string ResultBlobPath { get; set; }
    public string Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
