using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using EffiTex.Api.Storage;

namespace EffiTex.Api.Tests.Storage;

public class BlobStorageServiceTests
{
    private readonly Mock<BlobServiceClient> _blobServiceClient;
    private readonly Mock<BlobContainerClient> _uploadClient;
    private readonly Mock<BlobContainerClient> _inspectClient;
    private readonly Mock<BlobContainerClient> _executeClient;
    private readonly Mock<BlobClient> _uploadBlobClient;
    private readonly Mock<BlobClient> _inspectBlobClient;
    private readonly Mock<BlobClient> _executeBlobClient;
    private readonly IBlobStorageService _sut;

    public BlobStorageServiceTests()
    {
        _blobServiceClient = new Mock<BlobServiceClient>();
        _uploadClient = new Mock<BlobContainerClient>();
        _inspectClient = new Mock<BlobContainerClient>();
        _executeClient = new Mock<BlobContainerClient>();
        _uploadBlobClient = new Mock<BlobClient>();
        _inspectBlobClient = new Mock<BlobClient>();
        _executeBlobClient = new Mock<BlobClient>();

        _blobServiceClient.Setup(c => c.GetBlobContainerClient("effitex-upload")).Returns(_uploadClient.Object);
        _blobServiceClient.Setup(c => c.GetBlobContainerClient("effitex-inspect")).Returns(_inspectClient.Object);
        _blobServiceClient.Setup(c => c.GetBlobContainerClient("effitex-execute")).Returns(_executeClient.Object);

        _uploadClient.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(_uploadBlobClient.Object);
        _inspectClient.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(_inspectBlobClient.Object);
        _executeClient.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(_executeBlobClient.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["EFFITEX_UPLOAD_CONTAINER"] = "effitex-upload",
                ["EFFITEX_INSPECT_CONTAINER"] = "effitex-inspect",
                ["EFFITEX_EXECUTE_CONTAINER"] = "effitex-execute"
            })
            .Build();

        _sut = new BlobStorageService(_blobServiceClient.Object, configuration);
    }

    // --- Container routing tests ---

    [Fact]
    public async Task UploadSourceAsync_UsesUploadContainer()
    {
        var documentId = Guid.NewGuid().ToString();
        var content = new MemoryStream(new byte[] { 1, 2, 3 });

        _uploadBlobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.UploadSourceAsync(documentId, content);

        _uploadClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Once);
        _inspectClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
        _executeClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DownloadSourceAsync_UsesUploadContainer()
    {
        var documentId = Guid.NewGuid().ToString();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var downloadResult = Mock.Of<BlobDownloadInfo>(d => d.Content == stream);
        _uploadBlobClient
            .Setup(b => b.DownloadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(downloadResult, Mock.Of<Response>()));

        await _sut.DownloadSourceAsync(documentId);

        _uploadClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Once);
        _inspectClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
        _executeClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadInspectResultAsync_UsesInspectContainer()
    {
        var jobId = Guid.NewGuid().ToString();
        var content = new MemoryStream(new byte[] { 1, 2, 3 });

        _inspectBlobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.UploadInspectResultAsync(jobId, content);

        _inspectClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Once);
        _uploadClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
        _executeClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DownloadInspectResultAsync_UsesInspectContainer()
    {
        var jobId = Guid.NewGuid().ToString();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var downloadResult = Mock.Of<BlobDownloadInfo>(d => d.Content == stream);
        _inspectBlobClient
            .Setup(b => b.DownloadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(downloadResult, Mock.Of<Response>()));

        await _sut.DownloadInspectResultAsync(jobId);

        _inspectClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Once);
        _uploadClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
        _executeClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadExecuteResultAsync_UsesExecuteContainer()
    {
        var jobId = Guid.NewGuid().ToString();
        var content = new MemoryStream(new byte[] { 1, 2, 3 });

        _executeBlobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.UploadExecuteResultAsync(jobId, content);

        _executeClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Once);
        _uploadClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
        _inspectClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DownloadExecuteResultAsync_UsesExecuteContainer()
    {
        var jobId = Guid.NewGuid().ToString();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var downloadResult = Mock.Of<BlobDownloadInfo>(d => d.Content == stream);
        _executeBlobClient
            .Setup(b => b.DownloadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(downloadResult, Mock.Of<Response>()));

        await _sut.DownloadExecuteResultAsync(jobId);

        _executeClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Once);
        _uploadClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
        _inspectClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteSourceAsync_UsesUploadContainer()
    {
        var documentId = Guid.NewGuid().ToString();
        _uploadBlobClient
            .Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        await _sut.DeleteSourceAsync(documentId);

        _uploadClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Once);
        _inspectClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
        _executeClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteInspectResultAsync_UsesInspectContainer()
    {
        var jobId = Guid.NewGuid().ToString();
        _inspectBlobClient
            .Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        await _sut.DeleteInspectResultAsync(jobId);

        _inspectClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Once);
        _uploadClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
        _executeClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteExecuteResultAsync_UsesExecuteContainer()
    {
        var jobId = Guid.NewGuid().ToString();
        _executeBlobClient
            .Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        await _sut.DeleteExecuteResultAsync(jobId);

        _executeClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Once);
        _uploadClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
        _inspectClient.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    // --- Flat blob path tests ---

    [Fact]
    public async Task UploadSourceAsync_UsesFlatBlobPath_NoSourcePrefix()
    {
        var documentId = Guid.NewGuid().ToString();
        var content = new MemoryStream(new byte[] { 1, 2, 3 });

        _uploadBlobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.UploadSourceAsync(documentId, content);

        _uploadClient.Verify(c => c.GetBlobClient(
            It.Is<string>(p => !p.Contains('/') && p == $"{documentId}.pdf")),
            Times.Once);
    }

    [Fact]
    public async Task UploadInspectResultAsync_UsesFlatBlobPath_NoResultsPrefix()
    {
        var jobId = Guid.NewGuid().ToString();
        var content = new MemoryStream(new byte[] { 1, 2, 3 });

        _inspectBlobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.UploadInspectResultAsync(jobId, content);

        _inspectClient.Verify(c => c.GetBlobClient(
            It.Is<string>(p => !p.Contains('/') && p == $"{jobId}.json")),
            Times.Once);
    }

    [Fact]
    public async Task UploadExecuteResultAsync_UsesFlatBlobPath_NoResultsPrefix()
    {
        var jobId = Guid.NewGuid().ToString();
        var content = new MemoryStream(new byte[] { 1, 2, 3 });

        _executeBlobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.UploadExecuteResultAsync(jobId, content);

        _executeClient.Verify(c => c.GetBlobClient(
            It.Is<string>(p => !p.Contains('/') && p == $"{jobId}.pdf")),
            Times.Once);
    }
}
