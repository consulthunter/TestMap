using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities;

namespace TestMap.Persistence.Ef.Configuration;

public class ProjectEntityConfiguration : IEntityTypeConfiguration<ProjectEntity>
{
    public void Configure(EntityTypeBuilder<ProjectEntity> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Owner).HasColumnName("owner").IsRequired();
        builder.Property(x => x.RepoName).HasColumnName("repo_name").IsRequired();
        builder.Property(x => x.DirectoryPath).HasColumnName("directory_path").IsRequired();
        builder.Property(x => x.WebUrl).HasColumnName("web_url");
        builder.Property(x => x.DatabasePath).HasColumnName("database_path");
        builder.Property(x => x.LastAnalyzedCommit).HasColumnName("last_analyzed_commit");
        builder.Property(x => x.ContentHash).HasColumnName("content_hash");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}