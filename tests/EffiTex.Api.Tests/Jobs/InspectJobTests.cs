using FluentAssertions;
using Moq;
using Xunit;
using EffiTex.Api.Data;
using EffiTex.Api.Data.Entities;
using EffiTex.Api.Jobs;
using EffiTex.Api.Storage;
using EffiTex.Engine.Models.Inspect;

namespace EffiTex.Api.Tests.Jobs;

public class InspectJobTests
{
    private readonly Mock<IDocumentRepository> _repo;
    private readonly Mock<IBlobStorageService> _blobs;
    private readonly Mock<IInspectRunner> _runner;
    private readonly InspectJob _sut;

    public InspectJobTests()
    {
        _repo = new Mock<IDocumentRepository>();
        _blobs = new Mock<IBlobStorageService>();
        _runner = new Mock<IInspectRunner>();
        _sut = new InspectJob(_repo.Object, _blobs.Object, _runner.Object);
    }

    private static void setupDefaults(
        Mock<IDocumentRepository> repo,
        Mock<IBlobStorageService> blobs,
        Mock<IInspectRunner> runner,
        Guid jobId,
        JobEntity job)
    {
        repo.Setup(r => r.GetJobAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        repo.Setup(r => r.UpdateJobStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        blobs.Setup(b => b.DownloadSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        runner.Setup(h => h.Inspect(It.IsAny<Stream>()))
            .Returns(new InspectResponse());
        blobs.Setup(b => b.UploadInspectResultAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static JobEntity buildJob(Guid jobId, Guid docId) => new JobEntity
    {
        Id = jobId,
        DocumentId = docId,
        JobType = "inspect",
        Status = "pending",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task RunAsync_SetsStatusToProcessingBeforeDownload()
    {
        var jobId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var job = buildJob(jobId, docId);
        var callOrder = new List<string>();

        _repo.Setup(r => r.GetJobAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _repo.Setup(r => r.UpdateJobStatusAsync(jobId, "processing", null, null, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("processing"))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.UpdateJobStatusAsync(jobId, "complete", It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobs.Setup(b => b.DownloadSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("download"))
            .ReturnsAsync(new MemoryStream());
        _runner.Setup(h => h.Inspect(It.IsAny<Stream>())).Returns(new InspectResponse());
        _blobs.Setup(b => b.UploadInspectResultAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RunAsync(jobId, CancellationToken.None);

        callOrder.IndexOf("processing").Should().BeLessThan(callOrder.IndexOf("download"));
    }

    [Fact]
    public async Task RunAsync_DownloadsSourcePdfFromCorrectPath()
    {
        var jobId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var job = buildJob(jobId, docId);
        setupDefaults(_repo, _blobs, _runner, jobId, job);

        await _sut.RunAsync(jobId, CancellationToken.None);

        _blobs.Verify(b => b.DownloadSourceAsync(docId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_CallsInspectRunner()
    {
        var jobId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var job = buildJob(jobId, docId);
        setupDefaults(_repo, _blobs, _runner, jobId, job);

        await _sut.RunAsync(jobId, CancellationToken.None);

        _runner.Verify(h => h.Inspect(It.IsAny<Stream>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_UploadsResultJsonWithJobId()
    {
        var jobId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var job = buildJob(jobId, docId);
        setupDefaults(_repo, _blobs, _runner, jobId, job);

        await _sut.RunAsync(jobId, CancellationToken.None);

        _blobs.Verify(b => b.UploadInspectResultAsync(
            jobId.ToString(),
            It.IsAny<Stream>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_SetsStatusToCompleteWithJobId()
    {
        var jobId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var job = buildJob(jobId, docId);
        setupDefaults(_repo, _blobs, _runner, jobId, job);

        await _sut.RunAsync(jobId, CancellationToken.None);

        _repo.Verify(r => r.UpdateJobStatusAsync(
            jobId, "complete", jobId.ToString(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_SetsStatusToFailedOnException()
    {
        var jobId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var job = buildJob(jobId, docId);

        _repo.Setup(r => r.GetJobAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _repo.Setup(r => r.UpdateJobStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobs.Setup(b => b.DownloadSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RunAsync(jobId, CancellationToken.None));

        _repo.Verify(r => r.UpdateJobStatusAsync(
            jobId, "failed", null, "blob error", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ExceptionIsReThrownAfterSettingFailedStatus()
    {
        var jobId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var job = buildJob(jobId, docId);

        _repo.Setup(r => r.GetJobAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _repo.Setup(r => r.UpdateJobStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobs.Setup(b => b.DownloadSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("rethrow test"));

        var act = async () => await _sut.RunAsync(jobId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("rethrow test");
    }

    [Fact]
    public async Task RunAsync_JobNotFound_ReturnsWithoutWork()
    {
        var jobId = Guid.NewGuid();
        _repo.Setup(r => r.GetJobAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync((JobEntity)null);

        await _sut.RunAsync(jobId, CancellationToken.None);

        _blobs.Verify(b => b.DownloadSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
