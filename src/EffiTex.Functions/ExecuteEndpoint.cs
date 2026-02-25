using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace EffiTex.Functions;

public class ExecuteEndpoint
{
    private readonly BlobServiceClient _blobService;
    private readonly QueueServiceClient _queueService;
    private readonly InstructionDeserializer _deserializer;
    private readonly InstructionValidator _validator;
    private readonly ILogger<ExecuteEndpoint> _logger;

    public ExecuteEndpoint(
        BlobServiceClient blobService,
        QueueServiceClient queueService,
        InstructionDeserializer deserializer,
        InstructionValidator validator,
        ILogger<ExecuteEndpoint> logger)
    {
        _blobService = blobService;
        _queueService = queueService;
        _deserializer = deserializer;
        _validator = validator;
        _logger = logger;
    }

    [Function("ExecuteEndpoint")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "documents/{guid}/execute")]
        HttpRequestData req,
        string guid)
    {
        _logger.LogInformation("Execute request received for document {Guid}", guid);

        var contentType = req.Headers.TryGetValues("Content-Type", out var values)
            ? values.FirstOrDefault() ?? ""
            : "";

        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            var badReq = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badReq.WriteAsJsonAsync(new { error = "Request body is empty." });
            return badReq;
        }

        string callbackUrl = null;
        string instructionBody;
        string blobExtension;

        var isJson = contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

        if (isJson)
        {
            // JSON: callback_url is a sibling of the instruction data at the top level
            callbackUrl = extractCallbackUrlFromJson(body);
            instructionBody = body;
            blobExtension = ".json";
        }
        else
        {
            // YAML: callback_url is a top-level key
            callbackUrl = extractCallbackUrlFromYaml(body);
            instructionBody = body;
            blobExtension = ".yaml";
        }

        // Deserialize
        try
        {
            var instructionSet = _deserializer.Deserialize(instructionBody, contentType);

            // Validate
            var validationResult = _validator.Validate(instructionSet);
            if (!validationResult.IsValid)
            {
                var badReq = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(new
                {
                    error = "Validation failed.",
                    details = validationResult.Errors.Select(e => new { e.Field, e.Message })
                });
                return badReq;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize instructions for {Guid}", guid);
            var badReq = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badReq.WriteAsJsonAsync(new { error = $"Invalid instruction payload: {ex.Message}" });
            return badReq;
        }

        // Store raw instruction body in blob
        var containerClient = _blobService.GetBlobContainerClient("documents");
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient($"{guid}/instructions{blobExtension}");
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(instructionBody)))
        {
            await blobClient.UploadAsync(stream, overwrite: true);
        }

        // Enqueue job message
        var jobId = System.Guid.NewGuid().ToString("N");
        var jobMessage = new JobMessage
        {
            JobId = jobId,
            Guid = guid,
            CallbackUrl = callbackUrl
        };

        var queueClient = _queueService.GetQueueClient("execute-jobs");
        await queueClient.CreateIfNotExistsAsync();
        var messageJson = JsonSerializer.Serialize(jobMessage, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        await queueClient.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(messageJson)));

        _logger.LogInformation("Job {JobId} enqueued for document {Guid}", jobId, guid);

        // Return 202
        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { job_id = jobId });
        return response;
    }

    private static string extractCallbackUrlFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("callback_url", out var prop))
            {
                return prop.GetString();
            }
        }
        catch
        {
            // Ignore parse errors; the deserializer will catch them
        }

        return null;
    }

    private static string extractCallbackUrlFromYaml(string yaml)
    {
        // Simple line-based extraction for callback_url
        using var reader = new StringReader(yaml);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("callback_url:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Substring("callback_url:".Length).Trim();
                // Remove quotes if present
                if (value.Length >= 2 &&
                    ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                     (value.StartsWith("'") && value.EndsWith("'"))))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }
}
