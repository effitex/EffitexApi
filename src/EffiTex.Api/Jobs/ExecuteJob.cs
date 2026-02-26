using EffiTex.Api.Data;
using EffiTex.Api.Storage;
using EffiTex.Core.Deserialization;

namespace EffiTex.Api.Jobs;

public class ExecuteJob
{
    private readonly IDocumentRepository _repo;
    private readonly IBlobStorageService _blobs;
    private readonly IExecuteRunner _runner;
    private readonly InstructionDeserializer _deserializer;

    public ExecuteJob(IDocumentRepository repo, IBlobStorageService blobs, IExecuteRunner runner, InstructionDeserializer deserializer)
    {
        _repo = repo;
        _blobs = blobs;
        _runner = runner;
        _deserializer = deserializer;
    }

    public async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _repo.GetJobAsync(jobId, ct);
        if (job is null)
            return;

        await _repo.UpdateJobStatusAsync(jobId, "processing", null, null, ct);

        try
        {
            using var pdfStream = await _blobs.DownloadSourceAsync(job.DocumentId.ToString(), ct);

            var instructions = _deserializer.Deserialize(job.Dsl);

            var resultStream = _runner.Execute(pdfStream, instructions);

            var resultId = jobId.ToString();
            await _blobs.UploadExecuteResultAsync(resultId, resultStream, ct);

            await _repo.UpdateJobStatusAsync(jobId, "complete", resultId, null, ct);
        }
        catch (Exception ex)
        {
            await _repo.UpdateJobStatusAsync(jobId, "failed", null, ex.Message, ct);
            throw;
        }
    }
}
