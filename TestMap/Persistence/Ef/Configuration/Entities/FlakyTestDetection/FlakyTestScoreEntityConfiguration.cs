using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Persistence.Ef.Entities.FlakyTestDetection;

namespace TestMap.Persistence.Ef.Configuration.Entities.FlakyTestDetection;

public class FlakyTestScoreEntityConfiguration : IEntityTypeConfiguration<FlakyTestScoreEntity>
{
    public void Configure(EntityTypeBuilder<FlakyTestScoreEntity> builder)
    {
        builder.ToTable("flaky_test_scores");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RunId).HasColumnName("run_id").IsRequired();
        builder.Property(x => x.TestMemberId).HasColumnName("test_member_id");
        builder.Property(x => x.TestName).HasColumnName("test_name").IsRequired();
        builder.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
        builder.Property(x => x.FlakinessScore).HasColumnName("flakiness_score").IsRequired();
        builder.Property(x => x.Classification).HasColumnName("classification").HasConversion<string>().IsRequired();
        builder.Property(x => x.FactorScores)
            .HasColumnName("factor_scores")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<FlakinessFactorKind, double>>(v, (JsonSerializerOptions?)null) ?? new())
            .IsRequired();
        builder.Property(x => x.Weights)
            .HasColumnName("weights")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<FlakinessFactorKind, double>>(v, (JsonSerializerOptions?)null) ?? new())
            .IsRequired();
        builder.Property(x => x.Evidence)
            .HasColumnName("evidence")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new())
            .IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.RunId);
        builder.HasIndex(x => x.TestMemberId);
        builder.HasIndex(x => new { x.TestName, x.FilePath });
    }
}
