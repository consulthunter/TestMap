using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class TestExecutionEntityConfiguration : IEntityTypeConfiguration<TestExecutionEntity>
{
    public void Configure(EntityTypeBuilder<TestExecutionEntity> builder)
    {
        builder.ToTable("test_executions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.GenerationAttemptId).HasColumnName("generation_attempt_id").IsRequired();
        builder.Property(x => x.GeneratedTestCode).HasColumnName("generated_test_code");
        builder.Property(x => x.GeneratedTestMethodName).HasColumnName("generated_test_method_name");
        builder.Property(x => x.CompilationSucceeded).HasColumnName("compilation_succeeded").IsRequired();
        builder.Property(x => x.CompilationErrors).HasColumnName("compilation_errors");
        builder.Property(x => x.TestPassed).HasColumnName("test_passed").IsRequired();
        builder.Property(x => x.RuntimeErrors).HasColumnName("runtime_errors");
        builder.Property(x => x.AssertionErrors).HasColumnName("assertion_errors");
        builder.Property(x => x.ExecutionTimeMs).HasColumnName("execution_time_ms").IsRequired();
        builder.Property(x => x.FinalCoverage).HasColumnName("final_coverage").IsRequired();
        builder.Property(x => x.FinalCoveredLines).HasColumnName("final_covered_lines").IsRequired();
        builder.Property(x => x.FinalTotalLines).HasColumnName("final_total_lines").IsRequired();
        builder.Property(x => x.CoverageDelta).HasColumnName("coverage_delta").IsRequired();
        builder.Property(x => x.BaselineMutationScore).HasColumnName("baseline_mutation_score");
        builder.Property(x => x.MutationScoreAfter).HasColumnName("mutation_score_after");
        builder.Property(x => x.MutationScoreDelta).HasColumnName("mutation_score_delta");
        builder.Property(x => x.NewLinesCovered).HasColumnName("new_lines_covered").IsRequired();
        builder.Property(x => x.TestClassification).HasColumnName("test_classification").IsRequired();
        builder.Property(x => x.ExecutionTime).HasColumnName("execution_time").IsRequired();
        builder.Property(x => x.StructuredErrors).HasColumnName("structured_errors");
    }
}