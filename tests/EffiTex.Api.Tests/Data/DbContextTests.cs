using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using EffiTex.Api.Data;
using EffiTex.Api.Data.Entities;

namespace EffiTex.Api.Tests.Data;

public class DbContextTests
{
    private static EffiTexDbContext createInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<EffiTexDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new EffiTexDbContext(options);
    }

    [Fact]
    public async Task Documents_InsertAndReadBack_AllFieldsMatch()
    {
        await using var ctx = createInMemoryContext();
        var now = DateTimeOffset.UtcNow;
        var doc = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            BlobPath = "source/abc.pdf",
            FileName = "abc.pdf",
            FileSizeBytes = 9999L,
            UploadedAt = now,
            ExpiresAt = now.AddHours(24)
        };

        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var found = await ctx.Documents.FindAsync(doc.Id);
        found.Should().NotBeNull();
        found.BlobPath.Should().Be("source/abc.pdf");
        found.FileName.Should().Be("abc.pdf");
        found.FileSizeBytes.Should().Be(9999L);
        found.UploadedAt.Should().Be(now);
        found.ExpiresAt.Should().Be(now.AddHours(24));
    }

    [Fact]
    public async Task Jobs_InsertAndReadBackViaNavigation_LinkedToDocument()
    {
        await using var ctx = createInMemoryContext();
        var doc = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            BlobPath = "source/nav.pdf",
            FileName = "nav.pdf",
            FileSizeBytes = 100L,
            UploadedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            JobType = "inspect",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync();

        var found = await ctx.Jobs
            .Include(j => j.Document)
            .FirstAsync(j => j.Id == job.Id);
        found.Document.Should().NotBeNull();
        found.Document.Id.Should().Be(doc.Id);
    }

    [Fact]
    public async Task DeleteDocument_CascadeDeletesJobs()
    {
        await using var ctx = createInMemoryContext();
        var doc = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            BlobPath = "source/cascade.pdf",
            FileName = "cascade.pdf",
            FileSizeBytes = 200L,
            UploadedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            JobType = "execute",
            Status = "complete",
            CreatedAt = DateTimeOffset.UtcNow
        };
        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync();

        ctx.Documents.Remove(doc);
        await ctx.SaveChangesAsync();

        var jobExists = await ctx.Jobs.AnyAsync(j => j.Id == job.Id);
        jobExists.Should().BeFalse();
    }
}
