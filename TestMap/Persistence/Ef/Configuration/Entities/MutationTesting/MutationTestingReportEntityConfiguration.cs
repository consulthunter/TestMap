using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Results;
using TestMap.Persistence.Ef.Entities.MutationTesting;

namespace TestMap.Persistence.Ef.Configuration.Entities.MutationTesting;

public class MutationTestingReportEntityConfiguration : IEntityTypeConfiguration<MutationTestingReportEntity>
{
    public void Configure(EntityTypeBuilder<MutationTestingReportEntity> builder)
    {
        builder.ToTable("mutation_testing_reports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version").IsRequired();
        builder.Property(x => x.ProjectRoot).HasColumnName("project_root").IsRequired();
        builder.Property(x => x.MutationScore).HasColumnName("mutation_score").IsRequired();

        builder.Property(x => x.Files)
            .HasColumnName("files")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v =>
                    JsonSerializer.Deserialize<Dictionary<string, StrykerFileResult>>(v,
                        (JsonSerializerOptions?)null) ?? new Dictionary<string, StrykerFileResult>())
            .IsRequired();

        builder.Property(x => x.TestFiles)
            .HasColumnName("test_files")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, StrykerTestFileResult>>(v,
                    (JsonSerializerOptions?)null) ?? new Dictionary<string, StrykerTestFileResult>())
            .IsRequired();

        builder.Property(x => x.Thresholds)
            .HasColumnName("thresholds")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<StrykerThresholds>(v, (JsonSerializerOptions?)null) ??
                     new StrykerThresholds())
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}