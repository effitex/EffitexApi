using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;
using Xunit;
using EffiTex.Api.Data.Entities;

namespace EffiTex.Api.Tests.Endpoints;

public class JobSubmissionTests : IDisposable
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public JobSubmissionTests()
    {
        _factory = new ApiTestFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private DocumentEntity buildDocument(bool expired = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new DocumentEntity
        {
            Id = Guid.NewGuid(),
            BlobPath = "source/test.pdf",
            FileName = "test.pdf",
            FileSizeBytes = 1024L,
            UploadedAt = now.AddHours(-1),
            ExpiresAt = expired ? now.AddHours(-1) : now.AddHours(23)
        };
    }

    private const string VALID_YAML = "version: \"1.0\"\nmetadata:\n  title: Test\n";

    // --- Inspect endpoint ---

    [Fact]
    public async Task PostInspect_ValidDocument_Returns202WithJobInfo()
    {
        var doc = buildDocument();
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        _factory.MockRepo.Setup(r => r.CreateJobAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobEntity j, CancellationToken _) => j);

        var response = await _client.PostAsync($"/documents/{doc.Id}/inspect", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("job_type").GetString().Should().Be("inspect");
        json.GetProperty("status").GetString().Should().Be("pending");
        Guid.TryParse(json.GetProperty("job_id").GetString(), out _).Should().BeTrue();
        json.GetProperty("document_id").GetString().Should().Be(doc.Id.ToString());
    }

    [Fact]
    public async Task PostInspect_DocumentNotFound_Returns404()
    {
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentEntity)null);

        var response = await _client.PostAsync($"/documents/{Guid.NewGuid()}/inspect", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostInspect_ExpiredDocument_Returns404()
    {
        var doc = buildDocument(expired: true);
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var response = await _client.PostAsync($"/documents/{doc.Id}/inspect", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostInspect_ValidDocument_EnqueuesHangfireJobWithCorrectId()
    {
        var doc = buildDocument();
        Guid capturedJobId = Guid.Empty;
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        _factory.MockRepo.Setup(r => r.CreateJobAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()))
            .Callback<JobEntity, CancellationToken>((j, _) => capturedJobId = j.Id)
            .ReturnsAsync((JobEntity j, CancellationToken _) => j);

        await _client.PostAsync($"/documents/{doc.Id}/inspect", null);

        _factory.MockJobClient.Verify(c => c.Create(
            It.Is<Job>(j => j.Args.Contains(capturedJobId)),
            It.IsAny<IState>()), Times.Once);
    }

    [Fact]
    public async Task PostInspect_ValidDocument_InsertsJobWithPendingStatusAndInspectType()
    {
        var doc = buildDocument();
        JobEntity capturedJob = null;
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        _factory.MockRepo.Setup(r => r.CreateJobAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()))
            .Callback<JobEntity, CancellationToken>((j, _) => capturedJob = j)
            .ReturnsAsync((JobEntity j, CancellationToken _) => j);

        await _client.PostAsync($"/documents/{doc.Id}/inspect", null);

        capturedJob.Should().NotBeNull();
        capturedJob.Status.Should().Be("pending");
        capturedJob.JobType.Should().Be("inspect");
        capturedJob.Dsl.Should().BeNull();
    }

    // --- Execute endpoint ---

    [Fact]
    public async Task PostExecute_ValidYaml_Returns202WithJobInfo()
    {
        var doc = buildDocument();
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        _factory.MockRepo.Setup(r => r.CreateJobAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobEntity j, CancellationToken _) => j);

        var content = new StringContent(VALID_YAML, Encoding.UTF8, "application/x-yaml");
        var response = await _client.PostAsync($"/documents/{doc.Id}/execute", content);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("job_type").GetString().Should().Be("execute");
        json.GetProperty("status").GetString().Should().Be("pending");
    }

    [Fact]
    public async Task PostExecute_ValidJson_Returns202()
    {
        var doc = buildDocument();
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        _factory.MockRepo.Setup(r => r.CreateJobAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobEntity j, CancellationToken _) => j);

        var content = new StringContent("{\"version\":\"1.0\",\"metadata\":{\"title\":\"Test\"}}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/documents/{doc.Id}/execute", content);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostExecute_InvalidDsl_Returns400WithErrors()
    {
        var doc = buildDocument();
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var invalidYaml = "metadata:\n  tab_order: \"invalid_value\"\n";
        var content = new StringContent(invalidYaml, Encoding.UTF8, "application/x-yaml");
        var response = await _client.PostAsync($"/documents/{doc.Id}/execute", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostExecute_DocumentNotFound_Returns404()
    {
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentEntity)null);

        var content = new StringContent(VALID_YAML, Encoding.UTF8, "application/x-yaml");
        var response = await _client.PostAsync($"/documents/{Guid.NewGuid()}/execute", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostExecute_UnsupportedContentType_Returns415()
    {
        var doc = buildDocument();
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var content = new StringContent("something", Encoding.UTF8, "text/plain");
        var response = await _client.PostAsync($"/documents/{doc.Id}/execute", content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task PostExecute_ValidYaml_EnqueuesHangfireJobWithCorrectId()
    {
        var doc = buildDocument();
        Guid capturedJobId = Guid.Empty;
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        _factory.MockRepo.Setup(r => r.CreateJobAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()))
            .Callback<JobEntity, CancellationToken>((j, _) => capturedJobId = j.Id)
            .ReturnsAsync((JobEntity j, CancellationToken _) => j);

        var content = new StringContent(VALID_YAML, Encoding.UTF8, "application/x-yaml");
        await _client.PostAsync($"/documents/{doc.Id}/execute", content);

        _factory.MockJobClient.Verify(c => c.Create(
            It.Is<Job>(j => j.Args.Contains(capturedJobId)),
            It.IsAny<IState>()), Times.Once);
    }

    [Fact]
    public async Task PostExecute_ValidYaml_InsertsJobWithDslPopulated()
    {
        var doc = buildDocument();
        JobEntity capturedJob = null;
        _factory.MockRepo.Setup(r => r.GetDocumentAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        _factory.MockRepo.Setup(r => r.CreateJobAsync(It.IsAny<JobEntity>(), It.IsAny<CancellationToken>()))
            .Callback<JobEntity, CancellationToken>((j, _) => capturedJob = j)
            .ReturnsAsync((JobEntity j, CancellationToken _) => j);

        var content = new StringContent(VALID_YAML, Encoding.UTF8, "application/x-yaml");
        await _client.PostAsync($"/documents/{doc.Id}/execute", content);

        capturedJob.Should().NotBeNull();
        capturedJob.Dsl.Should().Be(VALID_YAML);
        capturedJob.JobType.Should().Be("execute");
        capturedJob.Status.Should().Be("pending");
    }
}
