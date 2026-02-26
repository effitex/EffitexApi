using EffiTex.Api.Data;
using EffiTex.Api.Data.Entities;
using EffiTex.Api.Storage;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Models;
using EffiTex.Core.Validation;
using Hangfire;
using EffiTex.Api.Jobs;

namespace EffiTex.Api.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        app.MapPost("/documents", uploadDocument);
        app.MapPost("/documents/{guid}/inspect", submitInspect);
        app.MapPost("/documents/{guid}/execute", submitExecute);
    }

    private static async Task<IResult> uploadDocument(
        HttpRequest request,
        IDocumentRepository repo,
        IBlobStorageService blobs,
        EffiTexOptions options)
    {
        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "A PDF file is required." });

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync();
        }
        catch
        {
            return Results.BadRequest(new { error = "A PDF file is required." });
        }

        var file = form.Files["file"];
        if (file == null || file.Length == 0)
            return Results.BadRequest(new { error = "A PDF file is required." });

        if (!isPdf(file))
            return Results.BadRequest(new { error = "File must be a PDF." });

        var documentId = Guid.NewGuid();
        var blobPath = $"source/{documentId}.pdf";

        await using var stream = file.OpenReadStream();
        await blobs.UploadAsync(blobPath, stream, "application/pdf");

        var now = DateTimeOffset.UtcNow;
        var doc = new DocumentEntity
        {
            Id = documentId,
            BlobPath = blobPath,
            FileName = file.FileName,
            FileSizeBytes = file.Length,
            UploadedAt = now,
            ExpiresAt = now.AddHours(options.TtlHours)
        };

        await repo.CreateDocumentAsync(doc);

        return Results.Accepted(value: new
        {
            document_id = documentId,
            expires_at = doc.ExpiresAt
        });
    }

    private static async Task<IResult> submitInspect(
        Guid guid,
        IDocumentRepository repo,
        IBackgroundJobClient jobClient)
    {
        var doc = await repo.GetDocumentAsync(guid);
        if (doc == null || doc.ExpiresAt < DateTimeOffset.UtcNow)
            return Results.NotFound(new { error = "Document not found or expired." });

        var jobId = Guid.NewGuid();
        var job = new JobEntity
        {
            Id = jobId,
            DocumentId = guid,
            JobType = "inspect",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repo.CreateJobAsync(job);
        jobClient.Enqueue<InspectJob>(j => j.RunAsync(jobId, CancellationToken.None));

        return Results.Accepted(value: new
        {
            job_id = jobId,
            document_id = guid,
            job_type = "inspect",
            status = "pending"
        });
    }

    private static async Task<IResult> submitExecute(
        Guid guid,
        HttpRequest request,
        IDocumentRepository repo,
        IBackgroundJobClient jobClient,
        InstructionDeserializer deserializer,
        InstructionValidator validator)
    {
        var doc = await repo.GetDocumentAsync(guid);
        if (doc == null || doc.ExpiresAt < DateTimeOffset.UtcNow)
            return Results.NotFound(new { error = "Document not found or expired." });

        var contentType = request.ContentType ?? string.Empty;
        if (!isYaml(contentType) && !isJson(contentType))
            return Results.StatusCode(415);

        string dsl;
        using (var reader = new StreamReader(request.Body))
            dsl = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(dsl))
            return Results.BadRequest(new { error = "Request body is required." });

        InstructionSet instructions;
        try
        {
            instructions = deserializer.Deserialize(dsl, contentType);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { errors = new[] { ex.Message } });
        }

        var validationResult = validator.Validate(instructions);
        if (!validationResult.IsValid)
        {
            return Results.BadRequest(new
            {
                errors = validationResult.Errors.Select(e => e.Message)
            });
        }

        var jobId = Guid.NewGuid();
        var job = new JobEntity
        {
            Id = jobId,
            DocumentId = guid,
            JobType = "execute",
            Status = "pending",
            Dsl = dsl,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repo.CreateJobAsync(job);
        jobClient.Enqueue<ExecuteJob>(j => j.RunAsync(jobId, CancellationToken.None));

        return Results.Accepted(value: new
        {
            job_id = jobId,
            document_id = guid,
            job_type = "execute",
            status = "pending"
        });
    }

    private static bool isPdf(IFormFile file)
    {
        var ext = System.IO.Path.GetExtension(file.FileName);
        if (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase)) return true;
        var ct = file.ContentType ?? string.Empty;
        return ct.Contains("pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool isYaml(string contentType) =>
        contentType.Contains("yaml", StringComparison.OrdinalIgnoreCase);

    private static bool isJson(string contentType) =>
        contentType.Contains("json", StringComparison.OrdinalIgnoreCase);
}
