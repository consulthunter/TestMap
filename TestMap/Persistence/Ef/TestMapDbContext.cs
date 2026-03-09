using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef.Entities;

namespace TestMap.Persistence.Ef;

public class TestMapDbContext : DbContext
{
    public TestMapDbContext(DbContextOptions<TestMapDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectEntity>(entity =>
        {
            entity.ToTable("projects");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Owner).HasColumnName("owner");
            entity.Property(x => x.RepoName).HasColumnName("repo_name");
            entity.Property(x => x.DirectoryPath).HasColumnName("directory_path");
            entity.Property(x => x.WebUrl).HasColumnName("web_url");
            entity.Property(x => x.DatabasePath).HasColumnName("database_path");
            entity.Property(x => x.LastAnalyzedCommit).HasColumnName("last_analyzed_commit");
            entity.Property(x => x.ContentHash).HasColumnName("content_hash");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}