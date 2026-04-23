using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class ExperimentRunEntityConfiguration : IEntityTypeConfiguration<ExperimentRunEntity>
{
    public void Configure(EntityTypeBuilder<ExperimentRunEntity> builder)
    {
        builder.ToTable("experiment_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(x => x.EndTime).HasColumnName("end_time");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.Configuration).HasColumnName("configuration").IsRequired();
        builder.Property(x => x.CandidateLimit).HasColumnName("candidate_limit").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();

        builder.HasMany(x => x.CandidateMethods)
            .WithOne(x => x.ExperimentRun)
            .HasForeignKey(x => x.ExperimentRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
