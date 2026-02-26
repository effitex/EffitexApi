using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using EffiTex.Api.Data;
using EffiTex.Api.Data.Entities;
using EffiTex.Api.Jobs;
using EffiTex.Api.Storage;

namespace EffiTex.Api.Tests.Jobs;

public class CleanupJobTests
{
    private readonly Mock<IDocumentRepository> _repo;
    private readonly Mock<IBlobStorageService> _blobs;
    private readonly CleanupJob _sut;

    public CleanupJobTests()
    {
        _repo = new Mock<IDocumentRepository>();
        _blobs = new Mock<IBlobStorageService>();
        _sut = new CleanupJob(_repo.Object, _blobs.Object, NullLogger<CleanupJob>.Instance);
    }

    private static DocumentEntity buildDocument(Guid? docId = null) => new DocumentEntity
    {
        Id = docId ?? Guid.NewGuid(),
        BlobPath = "source/test.pdf",
        FileName = "test.pdf",
        FileSizeBytes = 100L,
        UploadedAt = DateTimeOffset.UtcNow.AddHours(-48),
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
        Jobs = new List<JobEntity>()
    };

    [Fact]
    public async Task RunAsync_CallsGetExpiredDocumentsWithCurrentUtcTime()
    {
        var cutoff = DateTimeOffset.UtcNow;
        _repo.Setup(r => r.GetExpiredDocumentsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentEntity>());

        await _sut.RunAsync(CancellationToken.None);

        _repo.Verify(r => r.GetExpiredDocumentsAsync(
            It.Is<DateTimeOffset>(t => t >= cutoff && t <= cutoff.AddSeconds(5)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_DeletesResultBlobsForJobsWithResultBlobPath()
    {
        var doc = buildDocument();
        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            Status = "complete",
            ResultBlobPath = "results/abc.json"
        };
        doc.Jobs.Add(job);

        _repo.Setup(r => r.GetExpiredDocumentsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentEntity> { doc });
        _blobs.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeleteDocumentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RunAsync(CancellationToken.None);

        _blobs.Verify(b => b.DeleteAsync("results/abc.json", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_DeletesSourceBlobForEachExpiredDocument()
    {
        var docId = Guid.NewGuid();
        var doc = buildDocument(docId);

        _repo.Setup(r => r.GetExpiredDocumentsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentEntity> { doc });
        _blobs.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeleteDocumentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RunAsync(CancellationToken.None);

        _blobs.Verify(b => b.DeleteAsync($"source/{docId}.pdf", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_DeletesDocumentFromRepositoryForEachExpiredDocument()
    {
        var docId = Guid.NewGuid();
        var doc = buildDocument(docId);

        _repo.Setup(r => r.GetExpiredDocumentsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentEntity> { doc });
        _blobs.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeleteDocumentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RunAsync(CancellationToken.None);

        _repo.Verify(r => r.DeleteDocumentAsync(docId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_JobsWithNullResultBlobPathDoNotTriggerBlobDelete()
    {
        var doc = buildDocument();
        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            Status = "pending",
            ResultBlobPath = null
        };
        doc.Jobs.Add(job);

        _repo.Setup(r => r.GetExpiredDocumentsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentEntity> { doc });
        _blobs.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeleteDocumentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RunAsync(CancellationToken.None);

        _blobs.Verify(b => b.DeleteAsync(It.Is<string>(s => s.StartsWith("results/")), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_BlobDeleteThrows_ProcessingContinues()
    {
        var doc1 = buildDocument();
        var doc2 = buildDocument();

        _repo.Setup(r => r.GetExpiredDocumentsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentEntity> { doc1, doc2 });
        _blobs.Setup(b => b.DeleteAsync($"source/{doc1.Id}.pdf", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("blob missing"));
        _blobs.Setup(b => b.DeleteAsync($"source/{doc2.Id}.pdf", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeleteDocumentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RunAsync(CancellationToken.None);

        _repo.Verify(r => r.DeleteDocumentAsync(doc2.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
