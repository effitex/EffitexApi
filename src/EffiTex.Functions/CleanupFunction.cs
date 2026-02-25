using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EffiTex.Functions;

public class CleanupFunction
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CleanupFunction> _logger;

    public CleanupFunction(
        BlobServiceClient blobServiceClient,
        TableServiceClient tableServiceClient,
        IConfiguration configuration,
        ILogger<CleanupFunction> logger)
    {
        _blobServiceClient = blobServiceClient;
        _tableServiceClient = tableServiceClient;
        _configuration = configuration;
        _logger = logger;
    }

    [Function("Cleanup")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timer)
    {
        var ttlHours = GetTtlHours();
        var cutoff = DateTimeOffset.UtcNow.AddHours(-ttlHours);

        _logger.LogInformation(
            "Cleanup started. TTL={TtlHours}h, cutoff={Cutoff}",
            ttlHours, cutoff);

        await CleanupBlobsAsync(cutoff);
        await CleanupJobRecordsAsync(cutoff);

        _logger.LogInformation("Cleanup completed.");
    }

    internal int GetTtlHours()
    {
        var setting = _configuration["EFFITEX_TTL_HOURS"];
        if (!string.IsNullOrEmpty(setting) && int.TryParse(setting, out var hours) && hours > 0)
        {
            return hours;
        }
        return 24;
    }

    internal async Task CleanupBlobsAsync(DateTimeOffset cutoff)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient("documents");

        var sourceBlobs = new List<BlobItem>();
        await foreach (var blob in containerClient.GetBlobsAsync(
            BlobTraits.None, BlobStates.None, string.Empty, default))
        {
            if (blob.Name.EndsWith("/source.pdf", StringComparison.OrdinalIgnoreCase))
            {
                sourceBlobs.Add(blob);
            }
        }

        foreach (var sourceBlob in sourceBlobs)
        {
            if (sourceBlob.Properties.LastModified.HasValue
                && sourceBlob.Properties.LastModified.Value < cutoff)
            {
                var guidPrefix = sourceBlob.Name.Substring(
                    0, sourceBlob.Name.LastIndexOf('/'));

                _logger.LogInformation(
                    "Deleting expired document group: {Prefix}", guidPrefix);

                // Delete source.pdf
                await containerClient.GetBlobClient(sourceBlob.Name)
                    .DeleteIfExistsAsync();

                // Delete result.pdf if present
                await containerClient.GetBlobClient($"{guidPrefix}/result.pdf")
                    .DeleteIfExistsAsync();

                // Delete instructions.* if present - check common extensions
                await DeleteInstructionBlobsAsync(containerClient, guidPrefix);
            }
        }
    }

    private async Task DeleteInstructionBlobsAsync(
        BlobContainerClient containerClient, string guidPrefix)
    {
        await foreach (var blob in containerClient.GetBlobsAsync(
            BlobTraits.None, BlobStates.None, $"{guidPrefix}/instructions.", default))
        {
            await containerClient.GetBlobClient(blob.Name)
                .DeleteIfExistsAsync();
        }
    }

    internal async Task CleanupJobRecordsAsync(DateTimeOffset cutoff)
    {
        var tableClient = _tableServiceClient.GetTableClient("jobs");

        var cutoffString = cutoff.UtcDateTime.ToString("o");
        var filter = $"completed_at lt '{cutoffString}'";

        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter))
        {
            _logger.LogInformation(
                "Deleting expired job record: PK={PartitionKey}, RK={RowKey}",
                entity.PartitionKey, entity.RowKey);

            await tableClient.DeleteEntityAsync(
                entity.PartitionKey, entity.RowKey, entity.ETag);
        }
    }
}
