using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class GenerationAttemptEntityConfiguration : IEntityTypeConfiguration<GenerationAttemptEntity>
{
    public void Configure(EntityTypeBuilder<GenerationAttemptEntity> builder)
    {
        builder.ToTable("generation_attempts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CandidateMethodId).HasColumnName("candidate_method_id").IsRequired();
        builder.Property(x => x.ProviderName).HasColumnName("provider_name").IsRequired();
        builder.Property(x => x.ModelName).HasColumnName("model_name").IsRequired();
        builder.Property(x => x.Strategy).HasColumnName("strategy").IsRequired();
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(x => x.IsRepairAttempt).HasColumnName("is_repair_attempt").IsRequired();
        builder.Property(x => x.ParentAttemptId).HasColumnName("parent_attempt_id");
        builder.Property(x => x.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(x => x.EndTime).HasColumnName("end_time");
        builder.Property(x => x.TotalTokensUsed).HasColumnName("total_tokens_used").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.FailureKind).HasColumnName("failure_kind").IsRequired();
        builder.Property(x => x.FailureStage).HasColumnName("failure_stage").IsRequired();
        builder.Property(x => x.FailureCategory).HasColumnName("failure_category").IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").IsRequired();

        builder.HasOne(x => x.ParentAttempt)
            .WithMany()
            .HasForeignKey(x => x.ParentAttemptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.GenerationSteps)
            .WithOne(x => x.GenerationAttempt)
            .HasForeignKey(x => x.GenerationAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TestExecution)
            .WithOne(x => x.GenerationAttempt)
            .HasForeignKey<TestExecutionEntity>(x => x.GenerationAttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}