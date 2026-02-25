using System.Net;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Models;
using EffiTex.Core.Validation;
using EffiTex.Functions.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace EffiTex.Functions.Tests;

public class ExecuteJobFunctionTests
{
    private const string ValidYaml = @"
version: '1.0'
metadata:
  language: en
";

    private readonly Mock<BlobServiceClient> _blobServiceMock;
    private readonly Mock<TableServiceClient> _tableServiceMock;
    private readonly Mock<TableClient> _tableClientMock;
    private readonly InstructionDeserializer _deserializer;
    private readonly InstructionValidator _validator;
    private readonly Mock<IJobProcessor> _processorMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<ExecuteJobFunction>> _loggerMock;
    private readonly ExecuteJobFunction _function;
    private readonly List<JobStatusEntity> _upsertedEntities;

    public ExecuteJobFunctionTests()
    {
        _blobServiceMock = new Mock<BlobServiceClient>();
        _tableServiceMock = new Mock<TableServiceClient>();
        _tableClientMock = new Mock<TableClient>();
        _deserializer = new InstructionDeserializer();
        _validator = new InstructionValidator();
        _processorMock = new Mock<IJobProcessor>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<ExecuteJobFunction>>();
        _upsertedEntities = new List<JobStatusEntity>();

        // Default processor behavior: return a new stream with some content each time
        _processorMock
            .Setup(x => x.Execute(It.IsAny<Stream>(), It.IsAny<InstructionSet>()))
            .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes("fake-pdf-result")));

        setupTableMocks();

        _function = new ExecuteJobFunction(
            _blobServiceMock.Object,
            _tableServiceMock.Object,
            _deserializer,
            _validator,
            _processorMock.Object,
            _httpClientFactoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Run_LoadsPdfAndRunsProcessor_WritesResultToBlob()
    {
        var resultBlobMock = setupBlobMocks(ValidYaml);

        var message = createJobMessageJson("job1", "guid1", null);
        await _function.Run(message);

        resultBlobMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_OnSuccess_UpdatesTableStorageToCompleted()
    {
        setupBlobMocks(ValidYaml);

        var message = createJobMessageJson("job1", "guid1", null);
        await _function.Run(message);

        var completedEntity = _upsertedEntities.LastOrDefault(e => e.Status == "completed");
        completedEntity.Should().NotBeNull();
        completedEntity.JobId.Should().Be("job1");
        completedEntity.CompletedAt.Should().NotBeNull();
        completedEntity.Error.Should().BeNull();
    }

    [Fact]
    public async Task Run_OnSuccess_PostsCallbackWhenUrlProvided()
    {
        setupBlobMocks(ValidYaml);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var message = createJobMessageJson("job1", "guid1", "https://example.com/callback");
        await _function.Run(message);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri.ToString() == "https://example.com/callback"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Run_OnFailure_UpdatesTableStorageToFailed()
    {
        setupBlobMocks(ValidYaml);

        // Make processor throw
        _processorMock
            .Setup(x => x.Execute(It.IsAny<Stream>(), It.IsAny<InstructionSet>()))
            .Throws(new InvalidOperationException("Processing failed"));

        var message = createJobMessageJson("job1", "guid1", null);
        await _function.Run(message);

        var failedEntity = _upsertedEntities.LastOrDefault(e => e.Status == "failed");
        failedEntity.Should().NotBeNull();
        failedEntity.JobId.Should().Be("job1");
        failedEntity.Error.Should().NotBeNullOrEmpty();
        failedEntity.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Run_OnFailure_PostsFailureCallbackWhenUrlProvided()
    {
        setupBlobMocks(ValidYaml);

        // Make processor throw
        _processorMock
            .Setup(x => x.Execute(It.IsAny<Stream>(), It.IsAny<InstructionSet>()))
            .Throws(new InvalidOperationException("Processing failed"));

        HttpRequestMessage capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var message = createJobMessageJson("job1", "guid1", "https://example.com/callback");
        await _function.Run(message);

        capturedRequest.Should().NotBeNull();
        capturedRequest.Method.Should().Be(HttpMethod.Post);
        var bodyContent = await capturedRequest.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body = JsonDocument.Parse(bodyContent);
        body.RootElement.TryGetProperty("status", out var statusProp).Should().BeTrue();
        statusProp.GetString().Should().Be("failed");
        body.RootElement.TryGetProperty("error", out var errorProp).Should().BeTrue();
        errorProp.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Run_WithoutCallbackUrl_DoesNotPostCallback()
    {
        setupBlobMocks(ValidYaml);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var message = createJobMessageJson("job1", "guid1", null);
        await _function.Run(message);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    private Mock<BlobClient> setupBlobMocks(string instructionContent)
    {
        var instructionBytes = Encoding.UTF8.GetBytes(instructionContent);
        var pdfBytes = Encoding.UTF8.GetBytes("fake-pdf-content");

        var sourceBlobMock = new Mock<BlobClient>();
        sourceBlobMock
            .Setup(x => x.DownloadToAsync(It.IsAny<Stream>()))
            .Returns<Stream>(stream =>
            {
                stream.Write(pdfBytes, 0, pdfBytes.Length);
                stream.Position = 0;
                return Task.FromResult(Mock.Of<Response>());
            });
        sourceBlobMock
            .Setup(x => x.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>((stream, _) =>
            {
                stream.Write(pdfBytes, 0, pdfBytes.Length);
                stream.Position = 0;
                return Task.FromResult(Mock.Of<Response>());
            });

        var instructionBlobMock = new Mock<BlobClient>();
        instructionBlobMock
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
        instructionBlobMock
            .Setup(x => x.DownloadToAsync(It.IsAny<Stream>()))
            .Returns<Stream>(stream =>
            {
                stream.Write(instructionBytes, 0, instructionBytes.Length);
                stream.Position = 0;
                return Task.FromResult(Mock.Of<Response>());
            });
        instructionBlobMock
            .Setup(x => x.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>((stream, _) =>
            {
                stream.Write(instructionBytes, 0, instructionBytes.Length);
                stream.Position = 0;
                return Task.FromResult(Mock.Of<Response>());
            });

        var resultBlobMock = new Mock<BlobClient>();
        resultBlobMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        var nonExistentBlobMock = new Mock<BlobClient>();
        nonExistentBlobMock
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

        var containerMock = new Mock<BlobContainerClient>();
        containerMock
            .Setup(x => x.GetBlobClient(It.Is<string>(s => s.EndsWith("source.pdf"))))
            .Returns(sourceBlobMock.Object);
        containerMock
            .Setup(x => x.GetBlobClient(It.Is<string>(s => s.EndsWith("instructions.yaml"))))
            .Returns(instructionBlobMock.Object);
        containerMock
            .Setup(x => x.GetBlobClient(It.Is<string>(s => s.EndsWith("instructions.json"))))
            .Returns(nonExistentBlobMock.Object);
        containerMock
            .Setup(x => x.GetBlobClient(It.Is<string>(s => s.EndsWith("result.pdf"))))
            .Returns(resultBlobMock.Object);

        _blobServiceMock
            .Setup(x => x.GetBlobContainerClient("documents"))
            .Returns(containerMock.Object);

        return resultBlobMock;
    }

    private void setupTableMocks()
    {
        _tableClientMock
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Response.FromValue(
                new TableItem("JobStatus"), Mock.Of<Response>())));
        _tableClientMock
            .Setup(x => x.UpsertEntityAsync(
                It.IsAny<JobStatusEntity>(),
                It.IsAny<TableUpdateMode>(),
                It.IsAny<CancellationToken>()))
            .Callback<JobStatusEntity, TableUpdateMode, CancellationToken>((entity, _, _) =>
            {
                _upsertedEntities.Add(new JobStatusEntity
                {
                    PartitionKey = entity.PartitionKey,
                    RowKey = entity.RowKey,
                    JobId = entity.JobId,
                    Status = entity.Status,
                    CompletedAt = entity.CompletedAt,
                    Error = entity.Error
                });
            })
            .ReturnsAsync(Mock.Of<Response>());

        _tableServiceMock
            .Setup(x => x.GetTableClient("JobStatus"))
            .Returns(_tableClientMock.Object);
    }

    private static string createJobMessageJson(string jobId, string guid, string callbackUrl)
    {
        var msg = new { job_id = jobId, guid = guid, callback_url = callbackUrl };
        return JsonSerializer.Serialize(msg);
    }
}
