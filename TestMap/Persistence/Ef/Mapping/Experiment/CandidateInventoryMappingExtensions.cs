using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Mapping.Experiment;

public static class CandidateInventoryMappingExtensions
{
    public static CandidateInventoryItem ToDomain(this CandidateInventoryEntity entity)
    {
        return new CandidateInventoryItem
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            SourceMemberId = entity.SourceMemberId,
            SourceTestMappingId = entity.SourceTestMappingId,
            ExistingTestMemberId = entity.ExistingTestMemberId,
            SourceMethodName = entity.SourceMethodName,
            SourceMethodSignature = entity.SourceMethodSignature,
            ExistingTestMethodName = entity.ExistingTestMethodName,
            InitialCoverage = entity.InitialCoverage,
            ComplexityScore = entity.ComplexityScore,
            SelectionStrategy = Enum.TryParse<TargetSelectionStrategy>(entity.SelectionStrategy, true, out var strategy)
                ? strategy
                : TargetSelectionStrategy.Existing,
            ExistingTestOutcome = entity.ExistingTestOutcome,
            IsExperimentEligible = entity.IsExperimentEligible,
            IneligibilityReason = entity.IneligibilityReason,
            RiskScore = entity.RiskScore,
            MetricDrivenScore = entity.MetricDrivenScore,
            ExpectedMetricDelta = entity.ExpectedMetricDelta,
            MetricGuardrailStatus = entity.MetricGuardrailStatus,
            MetricSelectionReason = entity.MetricSelectionReason,
            TestState = Enum.TryParse<CandidateTestState>(entity.TestState, true, out var testState)
                ? testState
                : CandidateTestState.Unknown,
            RecommendedAction = Enum.TryParse<CandidateActionKind>(entity.RecommendedAction, true, out var action)
                ? action
                : CandidateActionKind.None,
            TestStateReason = entity.TestStateReason,
            SelectionTime = entity.SelectionTime,
            CreatedAt = entity.CreatedAt,
            BaselineRunId = entity.BaselineRunId,
            ContextEvidenceKind = entity.ContextEvidenceKind,
            ContextEvidenceSummary = entity.ContextEvidenceSummary,
            HasGroundedTestContext = entity.HasGroundedTestContext,
            MappedTestMemberId = entity.MappedTestMemberId,
            MappedTestMethodName = entity.MappedTestMethodName,
            AccessPathStrategy = entity.AccessPathStrategy,
            AccessPathMemberIdsJson = entity.AccessPathMemberIdsJson,
            TraceSummary = entity.TraceSummary,
            TraceJson = entity.TraceJson,
            TestIntentionsSummary = entity.TestIntentionsSummary,
            TypeConstructionSummary = entity.TypeConstructionSummary,
            CandidateMetadataJson = entity.CandidateMetadataJson
        };
    }

    public static CandidateInventoryEntity ToEntity(this CandidateInventoryItem item)
    {
        return new CandidateInventoryEntity
        {
            Id = item.Id,
            ProjectId = item.ProjectId,
            SourceMemberId = item.SourceMemberId,
            SourceTestMappingId = item.SourceTestMappingId,
            ExistingTestMemberId = item.ExistingTestMemberId,
            SourceMethodName = item.SourceMethodName,
            SourceMethodSignature = item.SourceMethodSignature,
            ExistingTestMethodName = item.ExistingTestMethodName,
            InitialCoverage = item.InitialCoverage,
            ComplexityScore = item.ComplexityScore,
            SelectionStrategy = item.SelectionStrategy.ToString(),
            ExistingTestOutcome = item.ExistingTestOutcome,
            IsExperimentEligible = item.IsExperimentEligible,
            IneligibilityReason = item.IneligibilityReason,
            RiskScore = item.RiskScore,
            MetricDrivenScore = item.MetricDrivenScore,
            ExpectedMetricDelta = item.ExpectedMetricDelta,
            MetricGuardrailStatus = item.MetricGuardrailStatus,
            MetricSelectionReason = item.MetricSelectionReason,
            TestState = item.TestState.ToString(),
            RecommendedAction = item.RecommendedAction.ToString(),
            TestStateReason = item.TestStateReason,
            SelectionTime = item.SelectionTime == default ? DateTime.UtcNow : item.SelectionTime,
            CreatedAt = item.CreatedAt == default ? DateTime.UtcNow : item.CreatedAt,
            BaselineRunId = item.BaselineRunId,
            ContextEvidenceKind = item.ContextEvidenceKind,
            ContextEvidenceSummary = item.ContextEvidenceSummary,
            HasGroundedTestContext = item.HasGroundedTestContext,
            MappedTestMemberId = item.MappedTestMemberId,
            MappedTestMethodName = item.MappedTestMethodName,
            AccessPathStrategy = item.AccessPathStrategy,
            AccessPathMemberIdsJson = item.AccessPathMemberIdsJson,
            TraceSummary = item.TraceSummary,
            TraceJson = item.TraceJson,
            TestIntentionsSummary = item.TestIntentionsSummary,
            TypeConstructionSummary = item.TypeConstructionSummary,
            CandidateMetadataJson = item.CandidateMetadataJson
        };
    }
}
