using FluentAssertions;
using Moq;
using Xunit;
using EffiTex.Api.Data;
using EffiTex.Api.Data.Entities;
using EffiTex.Api.Jobs;
using EffiTex.Api.Storage;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Models;

namespace EffiTex.Api.Tests.Jobs;

public class ExecuteJobTests
{
    private readonly Mock<IDocumentRepository> _repo;
    private readonly Mock<IBlobStorageService> _blobs;
    private readonly Mock<IExecuteRunner> _runner;
    private readonly InstructionDeserializer _deserializer;
    private readonly ExecuteJob _sut;

    private const string VALID_DSL = "version: \"1.0\"\nmetadata:\n  title: Test\n";

    public ExecuteJobTests()
    {
        _repo = new Mock<IDocumentRepository>();
        _blobs = new Mock<IBlobStorageService>();
        _runner = new Mock<IExecuteRunner>();
        _deserializer = new InstructionDeserializer();
        _sut = new ExecuteJob(_repo.Object, _blobs.Object, _runner.Object, _deserializer);
    }

    private static JobEntity buildJob(Guid jobId, Guid docId, string dsl = null) => new JobEntity
    {
        Id = jobId,
        DocumentId = docId,
        JobType = "execute",
        Status = "pending",
        Dsl = dsl ?? VALID_DSL,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private void setupDefaults(Guid jobId, JobEntity job)
    {
        _repo.Setup(r => r.GetJobAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _repo.Setup(r => r.UpdateJobStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobs.Setup(b => b.DownloadSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _runner.Setup(r => r.Execute(It.IsAny<Stream>(), It.IsAny<InstructionSet>()))
            .Returns(new MemoryStream(new byte[] { 1, 2, 3, 4 }));
        _blobs.Setup(b => b.UploadExecuteResultAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

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
        _runner.Setup(r => r.Execute(It.IsAny<Stream>(), It.IsAny<InstructionSet>()))
            .Returns(new MemoryStream(new byte[] { 1 }));
        _blobs.Setup(b => b.UploadExecuteResultAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
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
        setupDefaults(jobId, job);

        await _sut.RunAsync(jobId, CancellationToken.None);

        _blobs.Verify(b => b.DownloadSourceAsync(docId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_CallsExecuteRunnerWithDeserializedInstructions()
    {
        var jobId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var job = buildJob(jobId, docId);
        setupDefaults(jobId, job);

        await _sut.RunAsync(jobId, CancellationToken.None);

        _runner.Verify(r => r.Execute(
            It.IsAny<Stream>(),
            It.Is<InstructionSet>(s => s != null)), Times.Once);
    }

    [Fact]
    public async Task RunAsync_UploadsResultPdfWithJobId()
    {
        var jobId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var job = buildJob(jobId, docId);
        setupDefaults(jobId, job);

        await _sut.RunAsync(jobId, CancellationToken.None);

        _blobs.Verify(b => b.UploadExecuteResultAsync(
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
        setupDefaults(jobId, job);

        await _sut.RunAsync(jobId, CancellationToken.None);

        _repo.Verify(r => r.UpdateJobStatusAsync(
            jobId, "complete", jobId.ToString(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_RunnerExceptionSetsStatusToFailed()
    {
        var jobId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var job = buildJob(jobId, docId);

        _repo.Setup(r => r.GetJobAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _repo.Setup(r => r.UpdateJobStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobs.Setup(b => b.DownloadSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _runner.Setup(r => r.Execute(It.IsAny<Stream>(), It.IsAny<InstructionSet>()))
            .Throws(new InvalidOperationException("execute error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RunAsync(jobId, CancellationToken.None));

        _repo.Verify(r => r.UpdateJobStatusAsync(
            jobId, "failed", null, "execute error", It.IsAny<CancellationToken>()), Times.Once);
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
            .ReturnsAsync(new MemoryStream());
        _runner.Setup(r => r.Execute(It.IsAny<Stream>(), It.IsAny<InstructionSet>()))
            .Throws(new InvalidOperationException("rethrow test"));

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
