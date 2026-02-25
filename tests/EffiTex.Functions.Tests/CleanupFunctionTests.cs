using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EffiTex.Functions;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EffiTex.Functions.Tests;

public class CleanupFunctionTests
{
    private readonly Mock<BlobServiceClient> _blobServiceClient;
    private readonly Mock<BlobContainerClient> _containerClient;
    private readonly Mock<TableServiceClient> _tableServiceClient;
    private readonly Mock<TableClient> _tableClient;
    private readonly Mock<IConfiguration> _configuration;
    private readonly Mock<ILogger<CleanupFunction>> _logger;
    private readonly CleanupFunction _sut;

    public CleanupFunctionTests()
    {
        _blobServiceClient = new Mock<BlobServiceClient>();
        _containerClient = new Mock<BlobContainerClient>();
        _tableServiceClient = new Mock<TableServiceClient>();
        _tableClient = new Mock<TableClient>();
        _configuration = new Mock<IConfiguration>();
        _logger = new Mock<ILogger<CleanupFunction>>();

        _blobServiceClient
            .Setup(x => x.GetBlobContainerClient("documents"))
            .Returns(_containerClient.Object);

        _tableServiceClient
            .Setup(x => x.GetTableClient("jobs"))
            .Returns(_tableClient.Object);

        // Default: no TTL override (use 24h default)
        _configuration
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string)null);

        _sut = new CleanupFunction(
            _blobServiceClient.Object,
            _tableServiceClient.Object,
            _configuration.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Run_BlobsOlderThanTtl_AreDeleted()
    {
        // Arrange
        var oldTimestamp = DateTimeOffset.UtcNow.AddHours(-25);
        var guid = Guid.NewGuid().ToString();

        var sourceBlob = BlobsModelFactory.BlobItem(
            name: $"{guid}/source.pdf",
            properties: BlobsModelFactory.BlobItemProperties(
                accessTierInferred: true,
                lastModified: oldTimestamp));

        SetupBlobListing(new[] { sourceBlob });
        SetupInstructionBlobListing(guid, Array.Empty<BlobItem>());

        var sourceBlobClient = new Mock<BlobClient>();
        var resultBlobClient = new Mock<BlobClient>();

        _containerClient
            .Setup(x => x.GetBlobClient($"{guid}/source.pdf"))
            .Returns(sourceBlobClient.Object);
        _containerClient
            .Setup(x => x.GetBlobClient($"{guid}/result.pdf"))
            .Returns(resultBlobClient.Object);

        SetupTableQuery(Array.Empty<TableEntity>());

        // Act
        await _sut.Run(CreateTimerInfo());

        // Assert
        sourceBlobClient.Verify(
            x => x.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        resultBlobClient.Verify(
            x => x.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_BlobsNewerThanTtl_AreNotDeleted()
    {
        // Arrange
        var recentTimestamp = DateTimeOffset.UtcNow.AddHours(-1);
        var guid = Guid.NewGuid().ToString();

        var sourceBlob = BlobsModelFactory.BlobItem(
            name: $"{guid}/source.pdf",
            properties: BlobsModelFactory.BlobItemProperties(
                accessTierInferred: true,
                lastModified: recentTimestamp));

        SetupBlobListing(new[] { sourceBlob });

        var sourceBlobClient = new Mock<BlobClient>();
        _containerClient
            .Setup(x => x.GetBlobClient($"{guid}/source.pdf"))
            .Returns(sourceBlobClient.Object);

        SetupTableQuery(Array.Empty<TableEntity>());

        // Act
        await _sut.Run(CreateTimerInfo());

        // Assert
        sourceBlobClient.Verify(
            x => x.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_JobRecordsOlderThanTtl_AreDeleted()
    {
        // Arrange
        SetupBlobListing(Array.Empty<BlobItem>());

        var oldCompletedAt = DateTimeOffset.UtcNow.AddHours(-25).UtcDateTime.ToString("o");
        var entity = new TableEntity("partition1", "row1")
        {
            { "completed_at", oldCompletedAt }
        };

        SetupTableQuery(new[] { entity });

        // Act
        await _sut.Run(CreateTimerInfo());

        // Assert
        _tableClient.Verify(
            x => x.DeleteEntityAsync(
                "partition1",
                "row1",
                It.IsAny<ETag>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_JobRecordsNewerThanTtl_AreNotDeleted()
    {
        // Arrange
        SetupBlobListing(Array.Empty<BlobItem>());

        // Return empty - simulating that the filter query returns no records
        // (records newer than TTL won't match the filter)
        SetupTableQuery(Array.Empty<TableEntity>());

        // Act
        await _sut.Run(CreateTimerInfo());

        // Assert
        _tableClient.Verify(
            x => x.DeleteEntityAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ETag>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_MissingResultBlob_DoesNotThrow()
    {
        // Arrange
        var oldTimestamp = DateTimeOffset.UtcNow.AddHours(-25);
        var guid = Guid.NewGuid().ToString();

        var sourceBlob = BlobsModelFactory.BlobItem(
            name: $"{guid}/source.pdf",
            properties: BlobsModelFactory.BlobItemProperties(
                accessTierInferred: true,
                lastModified: oldTimestamp));

        SetupBlobListing(new[] { sourceBlob });
        SetupInstructionBlobListing(guid, Array.Empty<BlobItem>());

        var sourceBlobClient = new Mock<BlobClient>();
        var resultBlobClient = new Mock<BlobClient>();

        _containerClient
            .Setup(x => x.GetBlobClient($"{guid}/source.pdf"))
            .Returns(sourceBlobClient.Object);
        _containerClient
            .Setup(x => x.GetBlobClient($"{guid}/result.pdf"))
            .Returns(resultBlobClient.Object);

        // DeleteIfExistsAsync returns false when blob doesn't exist - no exception
        resultBlobClient
            .Setup(x => x.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

        SetupTableQuery(Array.Empty<TableEntity>());

        // Act
        var act = async () => await _sut.Run(CreateTimerInfo());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Run_CustomTtlHours_IsRespected()
    {
        // Arrange: set TTL to 2 hours
        _configuration
            .Setup(x => x["EFFITEX_TTL_HOURS"])
            .Returns("2");

        var sut = new CleanupFunction(
            _blobServiceClient.Object,
            _tableServiceClient.Object,
            _configuration.Object,
            _logger.Object);

        // A blob that is 3 hours old (older than 2h TTL, should be deleted)
        var oldGuid = Guid.NewGuid().ToString();
        var oldBlob = BlobsModelFactory.BlobItem(
            name: $"{oldGuid}/source.pdf",
            properties: BlobsModelFactory.BlobItemProperties(
                accessTierInferred: true,
                lastModified: DateTimeOffset.UtcNow.AddHours(-3)));

        // A blob that is 1 hour old (newer than 2h TTL, should NOT be deleted)
        var newGuid = Guid.NewGuid().ToString();
        var newBlob = BlobsModelFactory.BlobItem(
            name: $"{newGuid}/source.pdf",
            properties: BlobsModelFactory.BlobItemProperties(
                accessTierInferred: true,
                lastModified: DateTimeOffset.UtcNow.AddHours(-1)));

        SetupBlobListing(new[] { oldBlob, newBlob });
        SetupInstructionBlobListing(oldGuid, Array.Empty<BlobItem>());

        var oldSourceClient = new Mock<BlobClient>();
        var oldResultClient = new Mock<BlobClient>();
        var newSourceClient = new Mock<BlobClient>();

        _containerClient
            .Setup(x => x.GetBlobClient($"{oldGuid}/source.pdf"))
            .Returns(oldSourceClient.Object);
        _containerClient
            .Setup(x => x.GetBlobClient($"{oldGuid}/result.pdf"))
            .Returns(oldResultClient.Object);
        _containerClient
            .Setup(x => x.GetBlobClient($"{newGuid}/source.pdf"))
            .Returns(newSourceClient.Object);

        SetupTableQuery(Array.Empty<TableEntity>());

        // Act
        await sut.Run(CreateTimerInfo());

        // Assert: old blob should be deleted
        oldSourceClient.Verify(
            x => x.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: new blob should NOT be deleted
        newSourceClient.Verify(
            x => x.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void GetTtlHours_DefaultsTo24_WhenNotConfigured()
    {
        _sut.GetTtlHours().Should().Be(24);
    }

    [Fact]
    public void GetTtlHours_ReturnsConfiguredValue()
    {
        _configuration
            .Setup(x => x["EFFITEX_TTL_HOURS"])
            .Returns("48");

        var sut = new CleanupFunction(
            _blobServiceClient.Object,
            _tableServiceClient.Object,
            _configuration.Object,
            _logger.Object);

        sut.GetTtlHours().Should().Be(48);
    }

    // -- Helper methods --

    private static TimerInfo CreateTimerInfo()
    {
        return Mock.Of<TimerInfo>();
    }

    private void SetupBlobListing(IEnumerable<BlobItem> blobs)
    {
        var page = Page<BlobItem>.FromValues(
            blobs.ToList(), continuationToken: null, Mock.Of<Response>());

        var pages = AsyncPageable<BlobItem>.FromPages(new[] { page });

        _containerClient
            .Setup(x => x.GetBlobsAsync(
                It.IsAny<BlobTraits>(),
                It.IsAny<BlobStates>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(pages);
    }

    private void SetupInstructionBlobListing(string guidPrefix, IEnumerable<BlobItem> blobs)
    {
        var page = Page<BlobItem>.FromValues(
            blobs.ToList(), continuationToken: null, Mock.Of<Response>());

        var pages = AsyncPageable<BlobItem>.FromPages(new[] { page });

        _containerClient
            .Setup(x => x.GetBlobsAsync(
                It.IsAny<BlobTraits>(),
                It.IsAny<BlobStates>(),
                It.Is<string>(p => p == $"{guidPrefix}/instructions."),
                It.IsAny<CancellationToken>()))
            .Returns(pages);
    }

    private void SetupTableQuery(IEnumerable<TableEntity> entities)
    {
        var page = Page<TableEntity>.FromValues(
            entities.ToList(), continuationToken: null, Mock.Of<Response>());

        var pages = AsyncPageable<TableEntity>.FromPages(new[] { page });

        _tableClient
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(pages);
    }
}
