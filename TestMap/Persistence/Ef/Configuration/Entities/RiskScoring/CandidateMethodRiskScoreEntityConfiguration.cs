using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Persistence.Ef.Entities.RiskScoring;

namespace TestMap.Persistence.Ef.Configuration.Entities.RiskScoring;

public class CandidateMethodRiskScoreEntityConfiguration : IEntityTypeConfiguration<CandidateMethodRiskScoreEntity>
{
    public void Configure(EntityTypeBuilder<CandidateMethodRiskScoreEntity> builder)
    {
        builder.ToTable("candidate_method_risk_scores");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CandidateMethodId).HasColumnName("candidate_method_id");
        builder.Property(x => x.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(x => x.RiskScore).HasColumnName("risk_score").IsRequired();
        builder.Property(x => x.FactorScores)
            .HasColumnName("factor_scores")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<RiskFactorKind, double>>(v, (JsonSerializerOptions?)null) ??
                     new Dictionary<RiskFactorKind, double>())
            .IsRequired();
        builder.Property(x => x.Weights)
            .HasColumnName("weights")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<RiskFactorKind, double>>(v, (JsonSerializerOptions?)null) ??
                     new Dictionary<RiskFactorKind, double>())
            .IsRequired();
        builder.Property(x => x.SelectionReason).HasColumnName("selection_reason").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.MemberId);
        builder.HasIndex(x => x.CandidateMethodId);
    }
}