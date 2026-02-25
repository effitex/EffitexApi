using Azure;
using Azure.Data.Tables;
using EffiTex.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace EffiTex.Functions;

public class JobStatusFunction
{
    private readonly TableServiceClient _tableService;
    private readonly ILogger<JobStatusFunction> _logger;

    private const string TableName = "JobStatus";

    public JobStatusFunction(
        TableServiceClient tableService,
        ILogger<JobStatusFunction> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    [Function("JobStatusFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "jobs/{jobId}")]
        HttpRequestData req,
        string jobId)
    {
        _logger.LogInformation("Status request for job {JobId}", jobId);

        var tableClient = _tableService.GetTableClient(TableName);

        try
        {
            var entity = await tableClient.GetEntityAsync<JobStatusEntity>("Job", jobId);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                job_id = entity.Value.JobId,
                status = entity.Value.Status,
                completed_at = entity.Value.CompletedAt,
                error = entity.Value.Error
            });
            return response;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var response = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await response.WriteAsJsonAsync(new { error = $"Job {jobId} not found." });
            return response;
        }
    }
}
