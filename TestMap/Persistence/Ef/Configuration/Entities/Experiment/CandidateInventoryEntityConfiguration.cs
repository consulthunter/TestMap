using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Configuration.Entities.Experiment;

public class CandidateInventoryEntityConfiguration : IEntityTypeConfiguration<CandidateInventoryEntity>
{
    public void Configure(EntityTypeBuilder<CandidateInventoryEntity> builder)
    {
        builder.ToTable("candidate_inventory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.SourceMemberId).HasColumnName("source_member_id").IsRequired();
        builder.Property(x => x.SourceTestMappingId).HasColumnName("source_test_mapping_id");
        builder.Property(x => x.ExistingTestMemberId).HasColumnName("existing_test_member_id");
        builder.Property(x => x.SourceMethodName).HasColumnName("source_method_name").IsRequired();
        builder.Property(x => x.SourceMethodSignature).HasColumnName("source_method_signature").IsRequired();
        builder.Property(x => x.ExistingTestMethodName).HasColumnName("existing_test_method_name");
        builder.Property(x => x.InitialCoverage).HasColumnName("initial_coverage").IsRequired();
        builder.Property(x => x.ComplexityScore).HasColumnName("complexity_score").IsRequired();
        builder.Property(x => x.SelectionStrategy).HasColumnName("selection_strategy").IsRequired();
        builder.Property(x => x.ExistingTestOutcome).HasColumnName("existing_test_outcome");
        builder.Property(x => x.IsExperimentEligible).HasColumnName("is_experiment_eligible").IsRequired();
        builder.Property(x => x.IneligibilityReason).HasColumnName("ineligibility_reason");
        builder.Property(x => x.RiskScore).HasColumnName("risk_score");
        builder.Property(x => x.MetricDrivenScore).HasColumnName("metric_driven_score");
        builder.Property(x => x.ExpectedMetricDelta).HasColumnName("expected_metric_delta");
        builder.Property(x => x.MetricGuardrailStatus).HasColumnName("metric_guardrail_status");
        builder.Property(x => x.MetricSelectionReason).HasColumnName("metric_selection_reason");
        builder.Property(x => x.TestState).HasColumnName("test_state");
        builder.Property(x => x.RecommendedAction).HasColumnName("recommended_action");
        builder.Property(x => x.TestStateReason).HasColumnName("test_state_reason");
        builder.Property(x => x.SelectionTime).HasColumnName("selection_time").IsRequired();
        builder.Property(x => x.BaselineRunId).HasColumnName("baseline_run_id");
        builder.Property(x => x.ContextEvidenceKind).HasColumnName("context_evidence_kind");
        builder.Property(x => x.ContextEvidenceSummary).HasColumnName("context_evidence_summary");
        builder.Property(x => x.HasGroundedTestContext).HasColumnName("has_grounded_test_context").IsRequired();
        builder.Property(x => x.MappedTestMemberId).HasColumnName("mapped_test_member_id");
        builder.Property(x => x.MappedTestMethodName).HasColumnName("mapped_test_method_name");
        builder.Property(x => x.AccessPathStrategy).HasColumnName("access_path_strategy");
        builder.Property(x => x.AccessPathMemberIdsJson).HasColumnName("access_path_member_ids_json");
        builder.Property(x => x.TraceSummary).HasColumnName("trace_summary");
        builder.Property(x => x.TraceJson).HasColumnName("trace_json");
        builder.Property(x => x.TestIntentionsSummary).HasColumnName("test_intentions_summary");
        builder.Property(x => x.TypeConstructionSummary).HasColumnName("type_construction_summary");
        builder.Property(x => x.CandidateMetadataJson).HasColumnName("candidate_metadata_json");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => new { x.ProjectId, x.SelectionStrategy });
        builder.HasIndex(x => new { x.ProjectId, x.SelectionStrategy, x.IsExperimentEligible });
        builder.HasIndex(x => new { x.ProjectId, x.SelectionStrategy, x.ContextEvidenceKind });
        builder.HasIndex(x => x.SourceTestMappingId);

        builder.HasOne(x => x.SourceTestMapping)
            .WithMany()
            .HasForeignKey(x => x.SourceTestMappingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
