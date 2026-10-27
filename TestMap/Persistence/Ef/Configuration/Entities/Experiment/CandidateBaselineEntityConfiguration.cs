using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public sealed class CandidateBaselineEntityConfiguration : IEntityTypeConfiguration<CandidateBaselineEntity>
{
    public void Configure(EntityTypeBuilder<CandidateBaselineEntity> builder)
    {
        builder.ToTable("candidate_baselines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.StableKey).HasColumnName("stable_key").IsRequired();
        builder.Property(x => x.ExperimentRunId).HasColumnName("experiment_run_id").IsRequired();
        builder.Property(x => x.CandidateMethodId).HasColumnName("candidate_method_id").IsRequired();
        builder.Property(x => x.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(x => x.TestRunId).HasColumnName("test_run_id");
        builder.Property(x => x.Usable).HasColumnName("usable").IsRequired();
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.StableKey).IsUnique();
    }
}
