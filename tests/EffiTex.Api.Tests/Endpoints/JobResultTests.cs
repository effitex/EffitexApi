using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Xunit;
using EffiTex.Api.Data.Entities;

namespace EffiTex.Api.Tests.Endpoints;

public class JobResultTests : IDisposable
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public JobResultTests()
    {
        _factory = new ApiTestFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static DocumentEntity buildDocument() => new DocumentEntity
    {
        Id = Guid.NewGuid(),
        FileName = "original.pdf",
        BlobPath = "source/original.pdf",
        FileSizeBytes = 1024L,
        UploadedAt = DateTimeOffset.UtcNow.AddHours(-1),
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(23)
    };

    private static JobEntity buildJob(DocumentEntity doc, string status, string jobType = "inspect") => new JobEntity
    {
        Id = Guid.NewGuid(),
        DocumentId = doc.Id,
        Document = doc,
        JobType = jobType,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        CompletedAt = status is "complete" or "failed" ? DateTimeOffset.UtcNow : null,
        ResultBlobPath = status == "complete" ? $"results/test.{(jobType == "inspect" ? "json" : "pdf")}" : null
    };

    // --- Status endpoint ---

    [Fact]
    public async Task GetStatus_KnownJob_Returns200WithCorrectFields()
    {
        var doc = buildDocument();
        var job = buildJob(doc, "complete");
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        var response = await _client.GetAsync($"/jobs/{job.Id}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("job_id").GetString().Should().Be(job.Id.ToString());
        json.GetProperty("document_id").GetString().Should().Be(doc.Id.ToString());
        json.GetProperty("job_type").GetString().Should().Be("inspect");
        json.GetProperty("status").GetString().Should().Be("complete");
    }

    [Fact]
    public async Task GetStatus_UnknownJob_Returns404()
    {
        _factory.MockRepo.Setup(r => r.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobEntity)null);

        var response = await _client.GetAsync($"/jobs/{Guid.NewGuid()}/status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStatus_PendingJob_CompletedAtIsNull()
    {
        var doc = buildDocument();
        var job = buildJob(doc, "pending");
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        var response = await _client.GetAsync($"/jobs/{job.Id}/status");

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("completed_at").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetStatus_NonFailedJob_ErrorIsNull()
    {
        var doc = buildDocument();
        var job = buildJob(doc, "complete");
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        var response = await _client.GetAsync($"/jobs/{job.Id}/status");

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("error").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("processing")]
    [InlineData("complete")]
    [InlineData("failed")]
    public async Task GetStatus_AllStatusValues_ReturnedCorrectly(string status)
    {
        var doc = buildDocument();
        var job = buildJob(doc, status);
        if (status == "failed") job.Error = "boom";
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        var response = await _client.GetAsync($"/jobs/{job.Id}/status");

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("status").GetString().Should().Be(status);
    }

    // --- Result endpoint ---

    [Fact]
    public async Task GetResult_UnknownJob_Returns404()
    {
        _factory.MockRepo.Setup(r => r.GetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobEntity)null);

        var response = await _client.GetAsync($"/jobs/{Guid.NewGuid()}/result");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetResult_PendingJob_Returns409()
    {
        var doc = buildDocument();
        var job = buildJob(doc, "pending");
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        var response = await _client.GetAsync($"/jobs/{job.Id}/result");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetResult_ProcessingJob_Returns409()
    {
        var doc = buildDocument();
        var job = buildJob(doc, "processing");
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        var response = await _client.GetAsync($"/jobs/{job.Id}/result");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetResult_IncompleteJob_ResponseBodyIncludesCurrentStatus()
    {
        var doc = buildDocument();
        var job = buildJob(doc, "processing");
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        var response = await _client.GetAsync($"/jobs/{job.Id}/result");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("processing");
    }

    [Fact]
    public async Task GetResult_CompleteInspectJob_Returns200WithJsonContentType()
    {
        var doc = buildDocument();
        var job = buildJob(doc, "complete", "inspect");
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(job.DocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(doc);
        _factory.MockBlob.Setup(b => b.DownloadAsync(job.ResultBlobPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 123, 125 })); // {}

        var response = await _client.GetAsync($"/jobs/{job.Id}/result");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetResult_CompleteExecuteJob_Returns200WithPdfContentType()
    {
        var doc = buildDocument();
        var job = buildJob(doc, "complete", "execute");
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(job.DocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(doc);
        _factory.MockBlob.Setup(b => b.DownloadAsync(job.ResultBlobPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        var response = await _client.GetAsync($"/jobs/{job.Id}/result");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task GetResult_CompleteExecuteJob_ContentDispositionContainsRemediatedFilename()
    {
        var doc = buildDocument();
        var job = buildJob(doc, "complete", "execute");
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(job.DocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(doc);
        _factory.MockBlob.Setup(b => b.DownloadAsync(job.ResultBlobPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3, 4 }));

        var response = await _client.GetAsync($"/jobs/{job.Id}/result");

        var disposition = response.Content.Headers.ContentDisposition;
        disposition.Should().NotBeNull();
        disposition.FileName.Should().Contain("original_remediated.pdf");
    }

    [Fact]
    public async Task GetResult_CompleteInspectJob_ResponseBodyMatchesBlobContent()
    {
        var doc = buildDocument();
        var job = buildJob(doc, "complete", "inspect");
        var expectedBytes = "{\"file_hash\":\"abc\"}"u8.ToArray();
        _factory.MockRepo.Setup(r => r.GetJobAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(job.DocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(doc);
        _factory.MockBlob.Setup(b => b.DownloadAsync(job.ResultBlobPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(expectedBytes));

        var response = await _client.GetAsync($"/jobs/{job.Id}/result");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().Equal(expectedBytes);
    }
}
