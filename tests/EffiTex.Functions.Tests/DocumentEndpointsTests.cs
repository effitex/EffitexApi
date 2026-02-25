using System.Net;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EffiTex.Engine;
using EffiTex.Functions;
using EffiTex.Functions.Tests.Helpers;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Xunit;

namespace EffiTex.Functions.Tests;

public class DocumentEndpointsTests
{
    private readonly Mock<BlobServiceClient> _mockBlobService;
    private readonly Mock<BlobContainerClient> _mockContainerClient;
    private readonly InspectHandler _inspectHandler;
    private readonly DocumentEndpoints _endpoints;

    private static readonly string FixturesPath = System.IO.Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "fixtures");

    public DocumentEndpointsTests()
    {
        _mockBlobService = new Mock<BlobServiceClient>();
        _mockContainerClient = new Mock<BlobContainerClient>();
        _inspectHandler = new InspectHandler();

        _mockBlobService
            .Setup(x => x.GetBlobContainerClient("documents"))
            .Returns(_mockContainerClient.Object);

        _endpoints = new DocumentEndpoints(_mockBlobService.Object, _inspectHandler);
    }

    [Fact]
    public async Task UploadDocument_ReturnsOkWithGuid()
    {
        // Arrange
        var mockBlobClient = new Mock<BlobClient>();
        mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        _mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(
                It.IsAny<PublicAccessType>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContainerInfo>>());

        _mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        var pdfBytes = LoadFixturePdf("untagged_simple.pdf");
        var body = CreateMultipartBody("file", "test.pdf", pdfBytes);
        var request = CreateHttpRequest("POST", "http://localhost/api/documents", body.content, body.contentType);

        // Act
        var response = await _endpoints.UploadDocument(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await ReadResponseBody(response);
        var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var guidValue = result.GetProperty("guid").GetString();
        guidValue.Should().NotBeNullOrEmpty();
        Guid.TryParse(guidValue, out _).Should().BeTrue();
    }

    [Fact]
    public async Task UploadDocument_StoresFileInBlobStorage()
    {
        // Arrange
        var capturedBlobPath = string.Empty;
        var mockBlobClient = new Mock<BlobClient>();
        mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        _mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(
                It.IsAny<PublicAccessType>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContainerInfo>>());

        _mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Callback<string>(path => capturedBlobPath = path)
            .Returns(mockBlobClient.Object);

        var pdfBytes = LoadFixturePdf("untagged_simple.pdf");
        var body = CreateMultipartBody("file", "test.pdf", pdfBytes);
        var request = CreateHttpRequest("POST", "http://localhost/api/documents", body.content, body.contentType);

        // Act
        var response = await _endpoints.UploadDocument(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedBlobPath.Should().MatchRegex(@"^[0-9a-f\-]{36}/source\.pdf$");
        mockBlobClient.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InspectDocument_ReturnsOkWithValidJson()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var pdfBytes = LoadFixturePdf("untagged_simple.pdf");

        var mockBlobClient = new Mock<BlobClient>();
        SetupBlobExistsAndDownload(mockBlobClient, pdfBytes);

        _mockContainerClient
            .Setup(x => x.GetBlobClient($"{guid}/source.pdf"))
            .Returns(mockBlobClient.Object);

        var request = CreateHttpRequest("POST", $"http://localhost/api/documents/{guid}/inspect");

        // Act
        var response = await _endpoints.InspectDocument(request, guid);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await ReadResponseBody(response);
        responseBody.Should().NotBeNullOrEmpty();

        var json = JsonSerializer.Deserialize<JsonElement>(responseBody);
        json.TryGetProperty("fileHash", out _).Should().BeTrue();
        json.TryGetProperty("fileSizeBytes", out _).Should().BeTrue();
        json.TryGetProperty("pages", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetResult_Returns404WhenNoResultExists()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var mockBlobClient = new Mock<BlobClient>();
        var mockResponse = new Mock<Response<bool>>();
        mockResponse.Setup(x => x.Value).Returns(false);
        mockBlobClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        _mockContainerClient
            .Setup(x => x.GetBlobClient($"{guid}/result.pdf"))
            .Returns(mockBlobClient.Object);

        var request = CreateHttpRequest("GET", $"http://localhost/api/documents/{guid}/result");

        // Act
        var response = await _endpoints.GetResult(request, guid);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetResult_ReturnsPdfBytesWhenResultExists()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var pdfBytes = LoadFixturePdf("untagged_simple.pdf");

        var mockBlobClient = new Mock<BlobClient>();
        SetupBlobExistsAndDownload(mockBlobClient, pdfBytes);

        _mockContainerClient
            .Setup(x => x.GetBlobClient($"{guid}/result.pdf"))
            .Returns(mockBlobClient.Object);

        var request = CreateHttpRequest("GET", $"http://localhost/api/documents/{guid}/result");

        // Act
        var response = await _endpoints.GetResult(request, guid);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Body.Position = 0;
        using var ms = new MemoryStream();
        await response.Body.CopyToAsync(ms, TestContext.Current.CancellationToken);
        var resultBytes = ms.ToArray();
        resultBytes.Length.Should().BeGreaterThan(0);
        // Verify it starts with %PDF
        Encoding.ASCII.GetString(resultBytes, 0, 5).Should().Be("%PDF-");
    }

    // --- Helpers ---

    private static byte[] LoadFixturePdf(string fileName)
    {
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(FixturesPath, fileName));
        return File.ReadAllBytes(path);
    }

    private static void SetupBlobExistsAndDownload(Mock<BlobClient> mockBlobClient, byte[] content)
    {
        var mockExistsResponse = new Mock<Response<bool>>();
        mockExistsResponse.Setup(x => x.Value).Returns(true);
        mockBlobClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);

        // Setup both overloads of DownloadToAsync
        mockBlobClient
            .Setup(x => x.DownloadToAsync(It.IsAny<Stream>()))
            .Callback<Stream>((stream) =>
            {
                stream.Write(content, 0, content.Length);
                stream.Flush();
            })
            .ReturnsAsync(Mock.Of<Response>());

        mockBlobClient
            .Setup(x => x.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((stream, ct) =>
            {
                stream.Write(content, 0, content.Length);
                stream.Flush();
            })
            .ReturnsAsync(Mock.Of<Response>());
    }

    private static (byte[] content, string contentType) CreateMultipartBody(string fieldName, string fileName, byte[] fileContent)
    {
        var boundary = "----TestBoundary" + Guid.NewGuid().ToString("N");
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);

        writer.Write($"--{boundary}\r\n");
        writer.Write($"Content-Disposition: form-data; name=\"{fieldName}\"; filename=\"{fileName}\"\r\n");
        writer.Write("Content-Type: application/pdf\r\n");
        writer.Write("\r\n");
        writer.Flush();

        ms.Write(fileContent, 0, fileContent.Length);

        writer.Write($"\r\n--{boundary}--\r\n");
        writer.Flush();

        return (ms.ToArray(), $"multipart/form-data; boundary={boundary}");
    }

    private static HttpRequestData CreateHttpRequest(string method, string url, byte[] body = null, string contentType = null)
    {
        var context = new Mock<FunctionContext>();
        var serviceProvider = new Mock<IServiceProvider>();
        context.Setup(x => x.InstanceServices).Returns(serviceProvider.Object);

        var request = new MockHttpRequestData(context.Object, new Uri(url), body ?? Array.Empty<byte>());

        if (contentType != null)
        {
            request.Headers.Add("Content-Type", contentType);
        }

        return request;
    }

    private static async Task<string> ReadResponseBody(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
