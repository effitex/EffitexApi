using System.Net;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Models;
using EffiTex.Core.Validation;
using EffiTex.Functions.Tests.Helpers;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EffiTex.Functions.Tests;

public class ExecuteEndpointTests
{
    private const string ValidYaml = @"
version: '1.0'
metadata:
  language: en
";

    private const string InvalidYaml = @"
version: '2.0'
";

    private const string ValidYamlWithCallback = @"
callback_url: https://example.com/callback
version: '1.0'
metadata:
  language: en
";

    private const string ValidJson = @"{
  ""version"": ""1.0"",
  ""metadata"": { ""language"": ""en"" }
}";

    private const string ValidJsonWithCallback = @"{
  ""callback_url"": ""https://example.com/callback"",
  ""version"": ""1.0"",
  ""metadata"": { ""language"": ""en"" }
}";

    private readonly Mock<BlobServiceClient> _blobServiceMock;
    private readonly Mock<QueueServiceClient> _queueServiceMock;
    private readonly InstructionDeserializer _deserializer;
    private readonly InstructionValidator _validator;
    private readonly Mock<ILogger<ExecuteEndpoint>> _loggerMock;
    private readonly Mock<FunctionContext> _contextMock;
    private readonly ExecuteEndpoint _endpoint;
    private readonly Mock<QueueClient> _queueClientMock;
    private string _lastEnqueuedMessage;

    public ExecuteEndpointTests()
    {
        _blobServiceMock = new Mock<BlobServiceClient>();
        _queueServiceMock = new Mock<QueueServiceClient>();
        _deserializer = new InstructionDeserializer();
        _validator = new InstructionValidator();
        _loggerMock = new Mock<ILogger<ExecuteEndpoint>>();
        _contextMock = FunctionContextHelper.CreateMockContext();

        setupBlobMocks();
        _queueClientMock = setupQueueMocks();

        _endpoint = new ExecuteEndpoint(
            _blobServiceMock.Object,
            _queueServiceMock.Object,
            _deserializer,
            _validator,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Post_WithValidYaml_Returns202WithJobId()
    {
        var request = new MockHttpRequestData(_contextMock.Object, ValidYaml, "application/x-yaml");

        var response = await _endpoint.Run(request, "abc123");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = ((MockHttpResponseData)response).ReadBody();
        var result = JsonDocument.Parse(body);
        result.RootElement.TryGetProperty("job_id", out var jobIdProp).Should().BeTrue();
        jobIdProp.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Post_WithValidJson_Returns202WithJobId()
    {
        var request = new MockHttpRequestData(_contextMock.Object, ValidJson, "application/json");

        var response = await _endpoint.Run(request, "abc123");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = ((MockHttpResponseData)response).ReadBody();
        var result = JsonDocument.Parse(body);
        result.RootElement.TryGetProperty("job_id", out var jobIdProp).Should().BeTrue();
        jobIdProp.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Post_WithInvalidInstructions_Returns400WithErrors()
    {
        var request = new MockHttpRequestData(_contextMock.Object, InvalidYaml, "application/x-yaml");

        var response = await _endpoint.Run(request, "abc123");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = ((MockHttpResponseData)response).ReadBody();
        var result = JsonDocument.Parse(body);
        result.RootElement.TryGetProperty("error", out var errorProp).Should().BeTrue();
        errorProp.GetString().Should().Contain("Validation failed");
        result.RootElement.TryGetProperty("details", out var detailsProp).Should().BeTrue();
        detailsProp.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Post_WithEmptyBody_Returns400()
    {
        var request = new MockHttpRequestData(_contextMock.Object, "", "application/x-yaml");

        var response = await _endpoint.Run(request, "abc123");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WithValidYaml_StoresInstructionInBlob()
    {
        var blobClientMock = new Mock<BlobClient>();
        blobClientMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        var containerMock = new Mock<BlobContainerClient>();
        containerMock
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<Azure.Storage.Blobs.Models.PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContainerInfo>>());
        containerMock
            .Setup(x => x.GetBlobClient(It.Is<string>(s => s.Contains("instructions.yaml"))))
            .Returns(blobClientMock.Object);

        _blobServiceMock
            .Setup(x => x.GetBlobContainerClient("documents"))
            .Returns(containerMock.Object);

        var request = new MockHttpRequestData(_contextMock.Object, ValidYaml, "application/x-yaml");

        await _endpoint.Run(request, "testguid");

        blobClientMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Post_WithValidYaml_EnqueuesJobMessage()
    {
        var request = new MockHttpRequestData(_contextMock.Object, ValidYaml, "application/x-yaml");

        var response = await _endpoint.Run(request, "testguid");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _lastEnqueuedMessage.Should().NotBeNullOrEmpty();

        // Decode the base64 message and verify contents
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(_lastEnqueuedMessage));
        var jobMessage = JsonDocument.Parse(decoded);
        jobMessage.RootElement.TryGetProperty("job_id", out _).Should().BeTrue();
        jobMessage.RootElement.TryGetProperty("guid", out var guidProp).Should().BeTrue();
        guidProp.GetString().Should().Be("testguid");
    }

    [Fact]
    public async Task Post_WithYamlCallback_ExtractsCallbackUrl()
    {
        var request = new MockHttpRequestData(_contextMock.Object, ValidYamlWithCallback, "application/x-yaml");
        await _endpoint.Run(request, "testguid");

        _lastEnqueuedMessage.Should().NotBeNullOrEmpty();
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(_lastEnqueuedMessage));
        var jobMessage = JsonDocument.Parse(decoded);
        jobMessage.RootElement.TryGetProperty("callback_url", out var callbackProp).Should().BeTrue();
        callbackProp.GetString().Should().Be("https://example.com/callback");
    }

    [Fact]
    public async Task Post_WithJsonCallback_ExtractsCallbackUrl()
    {
        var request = new MockHttpRequestData(_contextMock.Object, ValidJsonWithCallback, "application/json");
        await _endpoint.Run(request, "testguid");

        _lastEnqueuedMessage.Should().NotBeNullOrEmpty();
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(_lastEnqueuedMessage));
        var jobMessage = JsonDocument.Parse(decoded);
        jobMessage.RootElement.TryGetProperty("callback_url", out var callbackProp).Should().BeTrue();
        callbackProp.GetString().Should().Be("https://example.com/callback");
    }

    private void setupBlobMocks()
    {
        var blobClientMock = new Mock<BlobClient>();
        blobClientMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        var containerMock = new Mock<BlobContainerClient>();
        containerMock
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<Azure.Storage.Blobs.Models.PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContainerInfo>>());
        containerMock
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(blobClientMock.Object);

        _blobServiceMock
            .Setup(x => x.GetBlobContainerClient("documents"))
            .Returns(containerMock.Object);
    }

    private Mock<QueueClient> setupQueueMocks()
    {
        var queueClientMock = new Mock<QueueClient>();
        queueClientMock
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response)null);

        // Set up all SendMessageAsync overloads to capture the message
        queueClientMock
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Callback<string>(msg => _lastEnqueuedMessage = msg)
            .ReturnsAsync(Mock.Of<Response<SendReceipt>>());
        queueClientMock
            .Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((msg, _) => _lastEnqueuedMessage = msg)
            .ReturnsAsync(Mock.Of<Response<SendReceipt>>());

        _queueServiceMock
            .Setup(x => x.GetQueueClient("execute-jobs"))
            .Returns(queueClientMock.Object);

        return queueClientMock;
    }
}
