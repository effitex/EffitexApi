using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Xunit;
using EffiTex.Api.Data.Entities;

namespace EffiTex.Api.Tests.Endpoints;

public class DocumentUploadTests : IDisposable
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public DocumentUploadTests()
    {
        _factory = new ApiTestFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static MultipartFormDataContent buildPdfUpload(string filename = "test.pdf", byte[] bytes = null)
    {
        bytes ??= new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", filename);
        return content;
    }

    [Fact]
    public async Task PostDocuments_ValidPdf_Returns202WithDocumentIdAndExpiresAt()
    {
        _factory.MockRepo
            .Setup(r => r.CreateDocumentAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentEntity d, CancellationToken _) => d);

        var response = await _client.PostAsync("/documents", buildPdfUpload());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        json.TryGetProperty("document_id", out var docId).Should().BeTrue();
        Guid.TryParse(docId.GetString(), out _).Should().BeTrue();
        json.TryGetProperty("expires_at", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PostDocuments_NoFile_Returns400()
    {
        var content = new MultipartFormDataContent();
        var response = await _client.PostAsync("/documents", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostDocuments_NonPdfFile_Returns400()
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test.txt");
        var response = await _client.PostAsync("/documents", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostDocuments_ValidPdf_CallsBlobUploadWithCorrectPath()
    {
        _factory.MockRepo
            .Setup(r => r.CreateDocumentAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentEntity d, CancellationToken _) => d);

        await _client.PostAsync("/documents", buildPdfUpload());

        _factory.MockBlob.Verify(b => b.UploadAsync(
            It.Is<string>(path => path.StartsWith("source/") && path.EndsWith(".pdf")),
            It.IsAny<Stream>(),
            "application/pdf",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostDocuments_ValidPdf_CallsCreateDocumentWithCorrectValues()
    {
        DocumentEntity capturedDoc = null;
        _factory.MockRepo
            .Setup(r => r.CreateDocumentAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentEntity, CancellationToken>((d, _) => capturedDoc = d)
            .ReturnsAsync((DocumentEntity d, CancellationToken _) => d);

        await _client.PostAsync("/documents", buildPdfUpload("my-file.pdf"));

        capturedDoc.Should().NotBeNull();
        capturedDoc.FileName.Should().Be("my-file.pdf");
        capturedDoc.FileSizeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PostDocuments_ValidPdf_ExpiresAtIsApproximatelyNowPlusTtl()
    {
        _factory.MockRepo
            .Setup(r => r.CreateDocumentAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentEntity d, CancellationToken _) => d);

        var before = DateTimeOffset.UtcNow;
        var response = await _client.PostAsync("/documents", buildPdfUpload());
        var after = DateTimeOffset.UtcNow;

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        var expiresAt = json.GetProperty("expires_at").GetDateTimeOffset();

        expiresAt.Should().BeAfter(before.AddHours(23));
        expiresAt.Should().BeBefore(after.AddHours(25));
    }
}
