using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Mapping.Experiment;

public static class CandidateMethodMappingExtensions
{
    public static CandidateMethod ToDomain(this CandidateMethodEntity entity)
    {
        return new CandidateMethod
        {
            Id = entity.Id,
            ExperimentRunId = entity.ExperimentRunId,
            MemberId = entity.SourceMemberId,
            ExistingTestMemberId = entity.ExistingTestMemberId,
            MethodName = entity.SourceMethodName,
            Signature = entity.SourceMethodSignature,
            ExistingTestMethodName = entity.ExistingTestMethodName,
            BaselineCoverage = entity.InitialCoverage,
            ComplexityScore = 0.0,
            MetricDrivenScore = entity.MetricDrivenScore,
            ExpectedMetricDelta = entity.ExpectedMetricDelta,
            MetricConfidence = entity.MetricConfidence,
            MetricFeasibility = entity.MetricFeasibility,
            MetricEstimatedCost = entity.MetricEstimatedCost,
            MetricGuardrailStatus = entity.MetricGuardrailStatus,
            MetricSelectionReason = entity.MetricSelectionReason,
            TestImprovementScore = entity.TestImprovementScore,
            TestImprovementReason = entity.TestImprovementReason,
            TestState = Enum.TryParse<CandidateTestState>(entity.TestState, true, out var testState)
                ? testState
                : CandidateTestState.Unknown,
            RecommendedAction = Enum.TryParse<CandidateActionKind>(entity.RecommendedAction, true, out var action)
                ? action
                : CandidateActionKind.None,
            TestStateReason = entity.TestStateReason,
            SelectionTime = entity.SelectionTime
        };
    }

    public static CandidateMethodEntity ToEntity(this CandidateMethod candidateMethod)
    {
        return new CandidateMethodEntity
        {
            Id = candidateMethod.Id,
            ExperimentRunId = candidateMethod.ExperimentRunId,
            SourceMemberId = candidateMethod.MemberId,
            ExistingTestMemberId = candidateMethod.ExistingTestMemberId,
            SourceMethodName = candidateMethod.MethodName,
            SourceMethodSignature = candidateMethod.Signature,
            ExistingTestMethodName = candidateMethod.ExistingTestMethodName ?? string.Empty,
            InitialCoverage = candidateMethod.BaselineCoverage,
            InitialCoveredLines = 0,
            InitialTotalLines = 0,
            MetricDrivenScore = candidateMethod.MetricDrivenScore,
            ExpectedMetricDelta = candidateMethod.ExpectedMetricDelta,
            MetricConfidence = candidateMethod.MetricConfidence,
            MetricFeasibility = candidateMethod.MetricFeasibility,
            MetricEstimatedCost = candidateMethod.MetricEstimatedCost,
            MetricGuardrailStatus = candidateMethod.MetricGuardrailStatus,
            MetricSelectionReason = candidateMethod.MetricSelectionReason,
            TestImprovementScore = candidateMethod.TestImprovementScore,
            TestImprovementReason = candidateMethod.TestImprovementReason,
            TestState = candidateMethod.TestState.ToString(),
            RecommendedAction = candidateMethod.RecommendedAction.ToString(),
            TestStateReason = candidateMethod.TestStateReason,
            SelectionTime = candidateMethod.SelectionTime == default ? DateTime.UtcNow : candidateMethod.SelectionTime
        };
    }
}