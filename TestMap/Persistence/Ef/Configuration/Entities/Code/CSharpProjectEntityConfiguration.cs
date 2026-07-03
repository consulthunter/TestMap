using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Configuration.Entities.Code;

public class CSharpProjectEntityConfiguration : IEntityTypeConfiguration<CSharpProjectEntity>
{
    public void Configure(EntityTypeBuilder<CSharpProjectEntity> builder)
    {
        builder.ToTable("csharp_projects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SolutionId).HasColumnName("solution_id").IsRequired();
        builder.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
        builder.Property(x => x.BuildTargets)
            .HasColumnName("build_targets")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .IsRequired();
        builder.Property(x => x.DefaultBuildTarget).HasColumnName("default_build_target").IsRequired();
        builder.Property(x => x.BuildMetadata)
            .HasColumnName("build_metadata")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<ProjectBuildMetadataModel>(v, (JsonSerializerOptions?)null) ??
                     new ProjectBuildMetadataModel())
            .IsRequired();
        builder.Property(x => x.ContentHash).HasColumnName("content_hash");
    }
}