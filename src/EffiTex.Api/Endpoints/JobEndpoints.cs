using System.Net.Mime;
using EffiTex.Api.Data;
using EffiTex.Api.Storage;

namespace EffiTex.Api.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        app.MapGet("/jobs/{jobId}/status", getStatus);
        app.MapGet("/jobs/{jobId}/result", getResult);
    }

    private static async Task<IResult> getStatus(
        Guid jobId,
        IDocumentRepository repo)
    {
        var job = await repo.GetJobAsync(jobId);
        if (job == null)
            return Results.NotFound(new { error = "Job not found." });

        return Results.Ok(new
        {
            job_id = job.Id,
            document_id = job.DocumentId,
            job_type = job.JobType,
            status = job.Status,
            created_at = job.CreatedAt,
            completed_at = job.CompletedAt,
            error = job.Error
        });
    }

    private static async Task<IResult> getResult(
        Guid jobId,
        IDocumentRepository repo,
        IBlobStorageService blobs,
        HttpContext httpContext)
    {
        var job = await repo.GetJobAsync(jobId);
        if (job == null)
            return Results.NotFound(new { error = "Job not found." });

        if (job.Status != "complete")
            return Results.Conflict(new { error = "Job is not complete.", status = job.Status });

        var stream = job.JobType == "inspect"
            ? await blobs.DownloadInspectResultAsync(job.ResultBlobPath)
            : await blobs.DownloadExecuteResultAsync(job.ResultBlobPath);

        if (job.JobType == "inspect")
        {
            httpContext.Response.Headers["Content-Type"] = "application/json";
            return Results.Stream(stream, "application/json");
        }
        else
        {
            var doc = await repo.GetDocumentAsync(job.DocumentId);
            var baseName = System.IO.Path.GetFileNameWithoutExtension(doc?.FileName ?? "document");
            var fileName = $"{baseName}_remediated.pdf";
            return Results.Stream(stream, "application/pdf",
                fileDownloadName: fileName);
        }
    }
}
