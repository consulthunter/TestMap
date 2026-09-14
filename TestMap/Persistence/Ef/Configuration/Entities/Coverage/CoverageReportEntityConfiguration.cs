using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Configuration.Entities.Coverage;

public class CoverageReportEntityConfiguration : IEntityTypeConfiguration<CoverageReportEntity>
{
    public void Configure(EntityTypeBuilder<CoverageReportEntity> builder)
    {
        builder.ToTable("coverage_reports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.TestRunId).HasColumnName("test_run_id");
        builder.Property(x => x.RunId).HasColumnName("run_id").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.CollectionStatus).HasColumnName("collection_status")
            .HasDefaultValue(CoverageReportModel.LegacyCollectionStatus).IsRequired();
        builder.Property(x => x.CollectionReason).HasColumnName("collection_reason").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.SuccessfulCollector).HasColumnName("successful_collector").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.CollectionMetadataJson).HasColumnName("collection_metadata_json").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.HasUsableCoverage).HasColumnName("has_usable_coverage").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.LineCountsAvailable).HasColumnName("line_counts_available").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.BranchCountsAvailable).HasColumnName("branch_counts_available").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.MeasurementPolicyVersion).HasColumnName("measurement_policy_version").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.RawObjectCount).HasColumnName("raw_object_count").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.MappedObjectCount).HasColumnName("mapped_object_count").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.RawMemberCount).HasColumnName("raw_member_count").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.MappedMemberCount).HasColumnName("mapped_member_count").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.LineRate).HasColumnName("line_rate").IsRequired();
        builder.Property(x => x.BranchRate).HasColumnName("branch_rate").IsRequired();
        builder.Property(x => x.Complexity).HasColumnName("complexity").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.Timestamp).HasColumnName("timestamp").IsRequired();
        builder.Property(x => x.LinesCovered).HasColumnName("lines_covered").IsRequired();
        builder.Property(x => x.LinesValid).HasColumnName("lines_valid").IsRequired();
        builder.Property(x => x.BranchesCovered).HasColumnName("branches_covered").IsRequired();
        builder.Property(x => x.BranchesValid).HasColumnName("branches_valid").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ScopeKind).HasColumnName("scope_kind").HasDefaultValue("Solution").IsRequired();
        builder.Property(x => x.ReportRole).HasColumnName("report_role")
            .HasDefaultValue(TestMap.Models.Testing.TestReportRole.RepositoryBaseline).IsRequired();
        builder.Property(x => x.ExperimentRunId).HasColumnName("experiment_run_id");
        builder.Property(x => x.SourceProjectPath).HasColumnName("source_project_path").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.TestProjectPath).HasColumnName("test_project_path").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(x => x.TargetFramework).HasColumnName("target_framework").HasDefaultValue(string.Empty).IsRequired();

        builder.HasOne(x => x.TestRun)
            .WithMany(x => x.CoverageReports)
            .HasForeignKey(x => x.TestRunId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.TestRunId);
        builder.HasIndex(x => new { x.ProjectId, x.ReportRole, x.ScopeKind });
        builder.HasIndex(x => new { x.ProjectId, x.RunId })
            .IsUnique()
            .HasFilter("run_id <> ''");
    }
}
