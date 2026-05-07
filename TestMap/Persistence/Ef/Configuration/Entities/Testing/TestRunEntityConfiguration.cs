using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Results;
using TestMap.Persistence.Ef.Entities.Testing;

namespace TestMap.Persistence.Ef.Configuration.Entities.Testing;

public class TestRunEntityConfiguration : IEntityTypeConfiguration<TestRunEntity>
{
    public void Configure(EntityTypeBuilder<TestRunEntity> builder)
    {
        builder.ToTable("test_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.RunId).HasColumnName("run_id").IsRequired();
        builder.Property(x => x.RunDate).HasColumnName("run_date").IsRequired();
        builder.Property(x => x.Success).HasColumnName("success").IsRequired();
        builder.Property(x => x.Coverage).HasColumnName("coverage").IsRequired();
        builder.Property(x => x.MutationScore).HasColumnName("mutation_score");
        builder.Property(x => x.LogPath).HasColumnName("log_path").IsRequired();
        builder.Property(x => x.FailureAnalysis)
            .HasColumnName("failure_analysis")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v)
                    ? null
                    : JsonSerializer.Deserialize<FailureAnalysisModel>(v, (JsonSerializerOptions?)null));
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}