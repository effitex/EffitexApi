using EffiTex.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EffiTex.Api.Data;

public class EffiTexDbContext : DbContext
{
    public EffiTexDbContext(DbContextOptions<EffiTexDbContext> options) : base(options) { }

    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
    public DbSet<JobEntity> Jobs => Set<JobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentEntity>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BlobPath).HasColumnName("blob_path");
            entity.Property(e => e.FileName).HasColumnName("file_name");
            entity.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("ix_documents_expires_at");
        });

        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.ToTable("jobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.JobType).HasColumnName("job_type").HasMaxLength(10);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20);
            entity.Property(e => e.Dsl).HasColumnName("dsl");
            entity.Property(e => e.ResultBlobPath).HasColumnName("result_blob_path");
            entity.Property(e => e.Error).HasColumnName("error");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.HasOne(e => e.Document)
                  .WithMany(d => d.Jobs)
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.DocumentId).HasDatabaseName("ix_jobs_document_id");
        });
    }
}
