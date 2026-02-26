using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using EffiTex.Api.Data;
using EffiTex.Api.Data.Entities;

namespace EffiTex.Api.Tests.Data;

public class DocumentRepositoryTests
{
    private static (EffiTexDbContext ctx, IDocumentRepository repo) createSut()
    {
        var options = new DbContextOptionsBuilder<EffiTexDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new EffiTexDbContext(options);
        var repo = new DocumentRepository(ctx);
        return (ctx, repo);
    }

    private static DocumentEntity buildDocument() => new DocumentEntity
    {
        Id = Guid.NewGuid(),
        BlobPath = $"source/{Guid.NewGuid()}.pdf",
        FileName = "test.pdf",
        FileSizeBytes = 1024L,
        UploadedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
    };

    [Fact]
    public async Task CreateDocumentAsync_PersistsAndReturnsEntity()
    {
        var (ctx, repo) = createSut();
        var doc = buildDocument();

        var result = await repo.CreateDocumentAsync(doc);

        result.Should().NotBeNull();
        result.Id.Should().Be(doc.Id);
        result.FileName.Should().Be("test.pdf");

        var inDb = await ctx.Documents.FindAsync(doc.Id);
        inDb.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDocumentAsync_ReturnsNullForNonExistentId()
    {
        var (_, repo) = createSut();

        var result = await repo.GetDocumentAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateJobAsync_PersistsAndReturnsEntityLinkedToDocument()
    {
        var (ctx, repo) = createSut();
        var doc = buildDocument();
        await repo.CreateDocumentAsync(doc);

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            JobType = "inspect",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await repo.CreateJobAsync(job);

        result.Should().NotBeNull();
        result.Id.Should().Be(job.Id);
        result.DocumentId.Should().Be(doc.Id);

        var inDb = await ctx.Jobs.FindAsync(job.Id);
        inDb.Should().NotBeNull();
    }

    [Fact]
    public async Task GetJobAsync_ReturnsNullForNonExistentId()
    {
        var (_, repo) = createSut();

        var result = await repo.GetJobAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateJobStatusAsync_SetsStatusResultBlobPathAndError()
    {
        var (ctx, repo) = createSut();
        var doc = buildDocument();
        await repo.CreateDocumentAsync(doc);

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            JobType = "execute",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repo.CreateJobAsync(job);

        await repo.UpdateJobStatusAsync(job.Id, "complete", "results/x.pdf", null);

        ctx.ChangeTracker.Clear();
        var updated = await ctx.Jobs.FindAsync(job.Id);
        updated.Status.Should().Be("complete");
        updated.ResultBlobPath.Should().Be("results/x.pdf");
        updated.Error.Should().BeNull();
    }

    [Fact]
    public async Task UpdateJobStatusAsync_SetsCompletedAtWhenStatusIsComplete()
    {
        var (ctx, repo) = createSut();
        var doc = buildDocument();
        await repo.CreateDocumentAsync(doc);

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            JobType = "inspect",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repo.CreateJobAsync(job);

        await repo.UpdateJobStatusAsync(job.Id, "complete");

        ctx.ChangeTracker.Clear();
        var updated = await ctx.Jobs.FindAsync(job.Id);
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateJobStatusAsync_SetsCompletedAtWhenStatusIsFailed()
    {
        var (ctx, repo) = createSut();
        var doc = buildDocument();
        await repo.CreateDocumentAsync(doc);

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            JobType = "execute",
            Status = "processing",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repo.CreateJobAsync(job);

        await repo.UpdateJobStatusAsync(job.Id, "failed", null, "Something went wrong");

        ctx.ChangeTracker.Clear();
        var updated = await ctx.Jobs.FindAsync(job.Id);
        updated.CompletedAt.Should().NotBeNull();
        updated.Error.Should().Be("Something went wrong");
    }

    [Fact]
    public async Task GetExpiredDocumentsAsync_ReturnsOnlyExpiredDocuments()
    {
        var (_, repo) = createSut();
        var cutoff = DateTimeOffset.UtcNow;

        var expired = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            BlobPath = "source/old.pdf",
            FileName = "old.pdf",
            FileSizeBytes = 100L,
            UploadedAt = cutoff.AddHours(-48),
            ExpiresAt = cutoff.AddHours(-1)
        };
        var active = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            BlobPath = "source/new.pdf",
            FileName = "new.pdf",
            FileSizeBytes = 100L,
            UploadedAt = cutoff.AddHours(-1),
            ExpiresAt = cutoff.AddHours(23)
        };
        await repo.CreateDocumentAsync(expired);
        await repo.CreateDocumentAsync(active);

        var result = await repo.GetExpiredDocumentsAsync(cutoff);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(expired.Id);
    }

    [Fact]
    public async Task DeleteDocumentAsync_RemovesDocumentAndItsJobs()
    {
        var (ctx, repo) = createSut();
        var doc = buildDocument();
        await repo.CreateDocumentAsync(doc);

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            JobType = "inspect",
            Status = "complete",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repo.CreateJobAsync(job);

        await repo.DeleteDocumentAsync(doc.Id);

        ctx.ChangeTracker.Clear();
        var docExists = await ctx.Documents.AnyAsync(d => d.Id == doc.Id);
        var jobExists = await ctx.Jobs.AnyAsync(j => j.Id == job.Id);
        docExists.Should().BeFalse();
        jobExists.Should().BeFalse();
    }
}
