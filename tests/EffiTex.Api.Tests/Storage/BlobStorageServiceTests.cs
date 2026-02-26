using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Moq;
using Xunit;
using EffiTex.Api.Storage;

namespace EffiTex.Api.Tests.Storage;

public class BlobStorageServiceTests
{
    private readonly Mock<BlobServiceClient> _blobServiceClient;
    private readonly Mock<BlobContainerClient> _containerClient;
    private readonly Mock<BlobClient> _blobClient;
    private readonly IBlobStorageService _sut;

    public BlobStorageServiceTests()
    {
        _blobServiceClient = new Mock<BlobServiceClient>();
        _containerClient = new Mock<BlobContainerClient>();
        _blobClient = new Mock<BlobClient>();

        _blobServiceClient
            .Setup(c => c.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_containerClient.Object);

        _containerClient
            .Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(_blobClient.Object);

        _containerClient
            .Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<BlobContainerInfo>)null);

        _sut = new BlobStorageService(_blobServiceClient.Object, "effitex");
    }

    [Fact]
    public async Task UploadAsync_CallsUploadOnCorrectBlobClient()
    {
        var path = "source/test.pdf";
        var content = new MemoryStream(new byte[] { 1, 2, 3 });

        _blobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        await _sut.UploadAsync(path, content, "application/pdf");

        _containerClient.Verify(c => c.GetBlobClient(path), Times.Once);
        _blobClient.Verify(b => b.UploadAsync(content, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsStreamFromBlobClient()
    {
        var path = "results/test.json";
        var expectedStream = new MemoryStream(new byte[] { 10, 20, 30 });

        var downloadResult = Mock.Of<BlobDownloadInfo>(d => d.Content == expectedStream);
        _blobClient
            .Setup(b => b.DownloadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(downloadResult, Mock.Of<Response>()));

        var result = await _sut.DownloadAsync(path);

        _containerClient.Verify(c => c.GetBlobClient(path), Times.Once);
        result.Should().BeSameAs(expectedStream);
    }

    [Fact]
    public async Task DeleteAsync_CallsDeleteIfExistsOnCorrectBlobClient()
    {
        var path = "source/delete-me.pdf";

        _blobClient
            .Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        await _sut.DeleteAsync(path);

        _containerClient.Verify(c => c.GetBlobClient(path), Times.Once);
        _blobClient.Verify(b => b.DeleteIfExistsAsync(DeleteSnapshotsOption.None, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueWhenBlobExists()
    {
        var path = "source/exists.pdf";

        _blobClient
            .Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        var result = await _sut.ExistsAsync(path);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseWhenBlobDoesNotExist()
    {
        var path = "source/missing.pdf";

        _blobClient
            .Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

        var result = await _sut.ExistsAsync(path);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IntegrationUploadDownloadDelete_RoundTrip()
    {
        var connectionString = Environment.GetEnvironmentVariable("EFFITEX_STORAGE_CONNECTION");
        if (connectionString == null) return;

        var container = Environment.GetEnvironmentVariable("EFFITEX_BLOB_CONTAINER") ?? "effitex-test";
        var client = new BlobServiceClient(connectionString);
        var service = new BlobStorageService(client, container);

        var path = $"test/{Guid.NewGuid()}.bin";
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        await service.UploadAsync(path, new MemoryStream(bytes), "application/octet-stream");

        var exists = await service.ExistsAsync(path);
        exists.Should().BeTrue();

        var downloaded = await service.DownloadAsync(path);
        var result = new MemoryStream();
        await downloaded.CopyToAsync(result);
        result.ToArray().Should().Equal(bytes);

        await service.DeleteAsync(path);

        var existsAfter = await service.ExistsAsync(path);
        existsAfter.Should().BeFalse();
    }
}
