using Azure;
using Azure.Data.Tables;

namespace EffiTex.Functions.Models;

public class JobStatusEntity : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string JobId { get; set; }
    public string Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Error { get; set; }
}
