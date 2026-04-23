using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class GenerationStepEntityConfiguration : IEntityTypeConfiguration<GenerationStepEntity>
{
    public void Configure(EntityTypeBuilder<GenerationStepEntity> builder)
    {
        builder.ToTable("generation_steps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.GenerationAttemptId).HasColumnName("generation_attempt_id").IsRequired();
        builder.Property(x => x.StepName).HasColumnName("step_name").IsRequired();
        builder.Property(x => x.StepOrder).HasColumnName("step_order").IsRequired();
        builder.Property(x => x.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(x => x.EndTime).HasColumnName("end_time");
        builder.Property(x => x.Prompt).HasColumnName("prompt");
        builder.Property(x => x.Response).HasColumnName("response");
        builder.Property(x => x.TokensUsed).HasColumnName("tokens_used").IsRequired();
        builder.Property(x => x.Success).HasColumnName("success").IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");
        builder.Property(x => x.ValidationResult).HasColumnName("validation_result");
    }
}
