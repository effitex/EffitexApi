using FluentAssertions;
using Xunit;
using EffiTex.Api.Data.Entities;

namespace EffiTex.Api.Tests.Data;

public class EntityTests
{
    [Fact]
    public void DocumentEntity_CanInstantiateWithAllProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var entity = new DocumentEntity
        {
            Id = id,
            BlobPath = "source/test.pdf",
            FileName = "test.pdf",
            FileSizeBytes = 12345L,
            UploadedAt = now,
            ExpiresAt = now.AddHours(24),
            Jobs = new List<JobEntity>()
        };

        entity.Id.Should().Be(id);
        entity.BlobPath.Should().Be("source/test.pdf");
        entity.FileName.Should().Be("test.pdf");
        entity.FileSizeBytes.Should().Be(12345L);
        entity.UploadedAt.Should().Be(now);
        entity.ExpiresAt.Should().Be(now.AddHours(24));
        entity.Jobs.Should().BeEmpty();
    }

    [Fact]
    public void JobEntity_CanInstantiateWithAllProperties()
    {
        var id = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var entity = new JobEntity
        {
            Id = id,
            DocumentId = docId,
            JobType = "inspect",
            Status = "pending",
            Dsl = "some dsl",
            ResultBlobPath = "results/test.json",
            Error = "some error",
            CreatedAt = now,
            CompletedAt = now.AddSeconds(10)
        };

        entity.Id.Should().Be(id);
        entity.DocumentId.Should().Be(docId);
        entity.JobType.Should().Be("inspect");
        entity.Status.Should().Be("pending");
        entity.Dsl.Should().Be("some dsl");
        entity.ResultBlobPath.Should().Be("results/test.json");
        entity.Error.Should().Be("some error");
        entity.CreatedAt.Should().Be(now);
        entity.CompletedAt.Should().Be(now.AddSeconds(10));
    }

    [Fact]
    public void JobEntity_Dsl_DefaultsToNull()
    {
        var entity = new JobEntity();
        entity.Dsl.Should().BeNull();
    }

    [Fact]
    public void JobEntity_ResultBlobPath_DefaultsToNull()
    {
        var entity = new JobEntity();
        entity.ResultBlobPath.Should().BeNull();
    }

    [Fact]
    public void JobEntity_CompletedAt_DefaultsToNull()
    {
        var entity = new JobEntity();
        entity.CompletedAt.Should().BeNull();
    }
}
