using System.Text;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EffiTex.Functions;

public class ExecuteJobFunction
{
    private readonly BlobServiceClient _blobService;
    private readonly TableServiceClient _tableService;
    private readonly InstructionDeserializer _deserializer;
    private readonly InstructionValidator _validator;
    private readonly IJobProcessor _processor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExecuteJobFunction> _logger;

    private const string TableName = "JobStatus";
    private const string ContainerName = "documents";

    public ExecuteJobFunction(
        BlobServiceClient blobService,
        TableServiceClient tableService,
        InstructionDeserializer deserializer,
        InstructionValidator validator,
        IJobProcessor processor,
        IHttpClientFactory httpClientFactory,
        ILogger<ExecuteJobFunction> logger)
    {
        _blobService = blobService;
        _tableService = tableService;
        _deserializer = deserializer;
        _validator = validator;
        _processor = processor;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [Function("ExecuteJobFunction")]
    public async Task Run(
        [QueueTrigger("execute-jobs")] string messageJson)
    {
        var message = JsonSerializer.Deserialize<JobMessage>(messageJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        _logger.LogInformation("Processing job {JobId} for document {Guid}", message.JobId, message.Guid);

        var tableClient = _tableService.GetTableClient(TableName);
        await tableClient.CreateIfNotExistsAsync(CancellationToken.None);

        // Set initial status
        var statusEntity = new JobStatusEntity
        {
            PartitionKey = "Job",
            RowKey = message.JobId,
            JobId = message.JobId,
            Status = "processing"
        };
        await tableClient.UpsertEntityAsync(statusEntity, cancellationToken: CancellationToken.None);

        try
        {
            var containerClient = _blobService.GetBlobContainerClient(ContainerName);

            // Load source PDF
            var sourceBlobClient = containerClient.GetBlobClient($"{message.Guid}/source.pdf");
            using var sourcePdfStream = new MemoryStream();
            await sourceBlobClient.DownloadToAsync(sourcePdfStream, CancellationToken.None);
            sourcePdfStream.Position = 0;

            // Load instruction body
            var instructionBody = await loadInstructionBody(containerClient, message.Guid);

            // Determine content type from blob extension
            var contentType = instructionBody.BlobName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? "application/json"
                : "application/x-yaml";

            // Deserialize and validate
            var instructionSet = _deserializer.Deserialize(instructionBody.Content, contentType);
            var validationResult = _validator.Validate(instructionSet);
            if (!validationResult.IsValid)
            {
                var errorMessages = string.Join("; ", validationResult.Errors.Select(e => $"{e.Field}: {e.Message}"));
                throw new InvalidOperationException($"Instruction validation failed: {errorMessages}");
            }

            // Execute
            using var resultStream = _processor.Execute(sourcePdfStream, instructionSet);

            // Write result PDF to blob
            var resultBlobClient = containerClient.GetBlobClient($"{message.Guid}/result.pdf");
            await resultBlobClient.UploadAsync(resultStream, overwrite: true, cancellationToken: CancellationToken.None);

            // Update status to completed
            statusEntity.Status = "completed";
            statusEntity.CompletedAt = DateTime.UtcNow;
            statusEntity.Error = null;
            await tableClient.UpsertEntityAsync(statusEntity, cancellationToken: CancellationToken.None);

            _logger.LogInformation("Job {JobId} completed successfully", message.JobId);

            // POST callback on success
            if (!string.IsNullOrEmpty(message.CallbackUrl))
            {
                await postCallback(message.CallbackUrl, new
                {
                    job_id = message.JobId,
                    status = "completed"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", message.JobId);

            // Update status to failed
            statusEntity.Status = "failed";
            statusEntity.CompletedAt = DateTime.UtcNow;
            statusEntity.Error = ex.Message;
            await tableClient.UpsertEntityAsync(statusEntity, cancellationToken: CancellationToken.None);

            // POST failure callback
            if (!string.IsNullOrEmpty(message.CallbackUrl))
            {
                await postCallback(message.CallbackUrl, new
                {
                    job_id = message.JobId,
                    status = "failed",
                    error = ex.Message
                });
            }
        }
    }

    private static async Task<InstructionBlobContent> loadInstructionBody(
        Azure.Storage.Blobs.BlobContainerClient containerClient,
        string guid)
    {
        // Try YAML first, then JSON
        var yamlBlobClient = containerClient.GetBlobClient($"{guid}/instructions.yaml");
        if (await yamlBlobClient.ExistsAsync(CancellationToken.None))
        {
            var content = await downloadBlobAsString(yamlBlobClient);
            return new InstructionBlobContent { Content = content, BlobName = "instructions.yaml" };
        }

        var jsonBlobClient = containerClient.GetBlobClient($"{guid}/instructions.json");
        if (await jsonBlobClient.ExistsAsync(CancellationToken.None))
        {
            var content = await downloadBlobAsString(jsonBlobClient);
            return new InstructionBlobContent { Content = content, BlobName = "instructions.json" };
        }

        throw new FileNotFoundException($"No instruction blob found for document {guid}.");
    }

    private static async Task<string> downloadBlobAsString(Azure.Storage.Blobs.BlobClient blobClient)
    {
        using var stream = new MemoryStream();
        await blobClient.DownloadToAsync(stream, CancellationToken.None);
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private async Task postCallback(string url, object payload)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await client.PostAsync(url, content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to POST callback to {Url}", url);
        }
    }

    private class InstructionBlobContent
    {
        public string Content { get; set; }
        public string BlobName { get; set; }
    }
}
