using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using EffiTex.Api.Data;
using EffiTex.Api.Data.Entities;

namespace EffiTex.Api.Tests.Data;

public class MigrationTests
{
    private static string getConnectionString()
    {
        return Environment.GetEnvironmentVariable("EFFITEX_PG_CONNECTION");
    }

    [Fact]
    public async Task Migration_AppliesCleanly_TablesAndIndexesExist()
    {
        var connectionString = getConnectionString();
        if (connectionString == null)
        {
            return; // skip if env var not set
        }

        var options = new DbContextOptionsBuilder<EffiTexDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var ctx = new EffiTexDbContext(options);
        await ctx.Database.MigrateAsync();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var documentsExists = await tableExistsAsync(conn, "documents");
        documentsExists.Should().BeTrue("documents table should exist after migration");

        var jobsExists = await tableExistsAsync(conn, "jobs");
        jobsExists.Should().BeTrue("jobs table should exist after migration");

        var expiresAtIdx = await indexExistsAsync(conn, "ix_documents_expires_at");
        expiresAtIdx.Should().BeTrue("ix_documents_expires_at index should exist");

        var docIdIdx = await indexExistsAsync(conn, "ix_jobs_document_id");
        docIdIdx.Should().BeTrue("ix_jobs_document_id index should exist");
    }

    [Fact]
    public async Task Migration_RoundTrip_InsertAndReadDocument()
    {
        var connectionString = getConnectionString();
        if (connectionString == null)
        {
            return; // skip if env var not set
        }

        var options = new DbContextOptionsBuilder<EffiTexDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var ctx = new EffiTexDbContext(options);
        await ctx.Database.MigrateAsync();

        var doc = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            BlobPath = $"source/{Guid.NewGuid()}.pdf",
            FileName = "migration-test.pdf",
            FileSizeBytes = 512L,
            UploadedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var found = await ctx.Documents.FindAsync(doc.Id);
        found.Should().NotBeNull();
        found.FileName.Should().Be("migration-test.pdf");

        ctx.Documents.Remove(found);
        await ctx.SaveChangesAsync();
    }

    private static async Task<bool> tableExistsAsync(NpgsqlConnection conn, string tableName)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @name",
            conn);
        cmd.Parameters.AddWithValue("name", tableName);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<bool> indexExistsAsync(NpgsqlConnection conn, string indexName)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND indexname = @name",
            conn);
        cmd.Parameters.AddWithValue("name", indexName);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }
}
