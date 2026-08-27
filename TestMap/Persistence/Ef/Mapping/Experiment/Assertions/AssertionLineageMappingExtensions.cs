using TestMap.Models.Experiment.Assertions;
using TestMap.Persistence.Ef.Entities.Experiment.Assertions;

namespace TestMap.Persistence.Ef.Mapping.Experiment.Assertions;

public static class AssertionLineageMappingExtensions
{
    public static AssertionLineageMeasurement ToDomain(this AssertionLineageMeasurementEntity entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ExperimentRunId = entity.ExperimentRunId,
        CandidateMethodId = entity.CandidateMethodId,
        GenerationAttemptId = entity.GenerationAttemptId,
        ToolAttemptId = entity.ToolAttemptId,
        ProducerLane = entity.ProducerLane,
        Status = Parse<AssertionMeasurementStatus>(entity.Status),
        FailureCode = entity.FailureCode,
        FailureReason = entity.FailureReason,
        PolicyVersion = entity.PolicyVersion,
        AssertionCatalogVersion = entity.AssertionCatalogVersion,
        MaxDepth = entity.MaxDepth,
        EligibleTestCount = entity.EligibleTestCount,
        AnalyzedTestCount = entity.AnalyzedTestCount,
        UnavailableTestCount = entity.UnavailableTestCount,
        NoRecognizedAssertionTestCount = entity.NoRecognizedAssertionTestCount,
        RecognizedAssertionCount = entity.RecognizedAssertionCount,
        UnrecognizedAssertionCount = entity.UnrecognizedAssertionCount,
        TracedAssertionCount = entity.TracedAssertionCount,
        TrivialAssertionCount = entity.TrivialAssertionCount,
        UnresolvedAssertionCount = entity.UnresolvedAssertionCount,
        AnalysisDurationMs = entity.AnalysisDurationMs,
        StartedAt = entity.StartedAt,
        CompletedAt = entity.CompletedAt,
        TestSummaries = entity.GeneratedTestSummaries
            .OrderBy(x => x.Id)
            .Select(ToDomain)
            .ToList()
    };

    public static AssertionLineageMeasurementEntity ToEntity(this AssertionLineageMeasurement model) => new()
    {
        Id = model.Id,
        ProjectId = model.ProjectId,
        ExperimentRunId = model.ExperimentRunId,
        CandidateMethodId = model.CandidateMethodId,
        GenerationAttemptId = model.GenerationAttemptId,
        ToolAttemptId = model.ToolAttemptId,
        ProducerLane = model.ProducerLane,
        Status = model.Status.ToString(),
        FailureCode = model.FailureCode,
        FailureReason = model.FailureReason,
        PolicyVersion = model.PolicyVersion,
        AssertionCatalogVersion = model.AssertionCatalogVersion,
        MaxDepth = model.MaxDepth,
        EligibleTestCount = model.EligibleTestCount,
        AnalyzedTestCount = model.AnalyzedTestCount,
        UnavailableTestCount = model.UnavailableTestCount,
        NoRecognizedAssertionTestCount = model.NoRecognizedAssertionTestCount,
        RecognizedAssertionCount = model.RecognizedAssertionCount,
        UnrecognizedAssertionCount = model.UnrecognizedAssertionCount,
        TracedAssertionCount = model.TracedAssertionCount,
        TrivialAssertionCount = model.TrivialAssertionCount,
        UnresolvedAssertionCount = model.UnresolvedAssertionCount,
        AnalysisDurationMs = model.AnalysisDurationMs,
        StartedAt = model.StartedAt,
        CompletedAt = model.CompletedAt,
        GeneratedTestSummaries = model.TestSummaries.Select(ToEntity).ToList()
    };

    public static GeneratedTestAssertionSummary ToDomain(this GeneratedTestAssertionSummaryEntity entity) => new()
    {
        Id = entity.Id,
        AssertionLineageMeasurementId = entity.AssertionLineageMeasurementId,
        TestMemberId = entity.TestMemberId,
        GeneratedTestExecutionId = entity.GeneratedTestExecutionId,
        ToolAttemptGeneratedTestId = entity.ToolAttemptGeneratedTestId,
        TestMethodName = entity.TestMethodName,
        TestFilePath = entity.TestFilePath,
        TestMemberContentHash = entity.TestMemberContentHash,
        FallbackIdentityHash = entity.FallbackIdentityHash,
        Status = Parse<GeneratedTestAssertionStatus>(entity.Status),
        StatusReason = entity.StatusReason,
        RecognizedAssertionCount = entity.RecognizedAssertionCount,
        UnrecognizedAssertionCount = entity.UnrecognizedAssertionCount,
        TracedAssertionCount = entity.TracedAssertionCount,
        TrivialAssertionCount = entity.TrivialAssertionCount,
        UnresolvedAssertionCount = entity.UnresolvedAssertionCount,
        Observations = entity.AssertionObservations
            .OrderBy(x => x.Ordinal)
            .Select(ToDomain)
            .ToList()
    };

    public static GeneratedTestAssertionSummaryEntity ToEntity(this GeneratedTestAssertionSummary model) => new()
    {
        Id = model.Id,
        AssertionLineageMeasurementId = model.AssertionLineageMeasurementId,
        TestMemberId = model.TestMemberId,
        GeneratedTestExecutionId = model.GeneratedTestExecutionId,
        ToolAttemptGeneratedTestId = model.ToolAttemptGeneratedTestId,
        TestMethodName = model.TestMethodName,
        TestFilePath = model.TestFilePath,
        TestMemberContentHash = model.TestMemberContentHash,
        FallbackIdentityHash = model.FallbackIdentityHash,
        Status = model.Status.ToString(),
        StatusReason = model.StatusReason,
        RecognizedAssertionCount = model.RecognizedAssertionCount,
        UnrecognizedAssertionCount = model.UnrecognizedAssertionCount,
        TracedAssertionCount = model.TracedAssertionCount,
        TrivialAssertionCount = model.TrivialAssertionCount,
        UnresolvedAssertionCount = model.UnresolvedAssertionCount,
        AssertionObservations = model.Observations.Select(ToEntity).ToList()
    };

    public static AssertionObservation ToDomain(this AssertionObservationEntity entity) => new()
    {
        Id = entity.Id,
        GeneratedTestAssertionSummaryId = entity.GeneratedTestAssertionSummaryId,
        InvocationId = entity.InvocationId,
        Ordinal = entity.Ordinal,
        Framework = entity.Framework,
        AssertionMethod = entity.AssertionMethod,
        RecognitionKind = Parse<AssertionRecognitionKind>(entity.RecognitionKind),
        FilePath = entity.FilePath,
        StartLine = entity.StartLine,
        StartColumn = entity.StartColumn,
        EndLine = entity.EndLine,
        EndColumn = entity.EndColumn,
        ExpressionHash = entity.ExpressionHash,
        Category = Parse<AssertionLineageCategory>(entity.Category),
        ResolutionCode = entity.ResolutionCode,
        DepthReached = entity.DepthReached,
        TargetRelation = Parse<AssertionTargetRelation>(entity.TargetRelation),
        TraceSummary = entity.TraceSummary,
        Steps = entity.LineageSteps
            .OrderBy(x => x.InputIndex)
            .ThenBy(x => x.PathIndex)
            .ThenBy(x => x.StepIndex)
            .Select(ToDomain)
            .ToList()
    };

    public static AssertionObservationEntity ToEntity(this AssertionObservation model) => new()
    {
        Id = model.Id,
        GeneratedTestAssertionSummaryId = model.GeneratedTestAssertionSummaryId,
        InvocationId = model.InvocationId,
        Ordinal = model.Ordinal,
        Framework = model.Framework,
        AssertionMethod = model.AssertionMethod,
        RecognitionKind = model.RecognitionKind.ToString(),
        FilePath = model.FilePath,
        StartLine = model.StartLine,
        StartColumn = model.StartColumn,
        EndLine = model.EndLine,
        EndColumn = model.EndColumn,
        ExpressionHash = model.ExpressionHash,
        Category = model.Category.ToString(),
        ResolutionCode = model.ResolutionCode,
        DepthReached = model.DepthReached,
        TargetRelation = model.TargetRelation.ToString(),
        TraceSummary = model.TraceSummary,
        LineageSteps = model.Steps.Select(ToEntity).ToList()
    };

    public static AssertionLineageStep ToDomain(this AssertionLineageStepEntity entity) => new()
    {
        Id = entity.Id,
        AssertionObservationId = entity.AssertionObservationId,
        InputIndex = entity.InputIndex,
        PathIndex = entity.PathIndex,
        StepIndex = entity.StepIndex,
        StepKind = Parse<AssertionLineageStepKind>(entity.StepKind),
        Depth = entity.Depth,
        SymbolDisplay = entity.SymbolDisplay,
        MemberId = entity.MemberId,
        FilePath = entity.FilePath,
        StartLine = entity.StartLine,
        StartColumn = entity.StartColumn,
        EndLine = entity.EndLine,
        EndColumn = entity.EndColumn,
        Outcome = Parse<AssertionLineageStepOutcome>(entity.Outcome),
        ReasonCode = entity.ReasonCode,
        Summary = entity.Summary
    };

    public static AssertionLineageStepEntity ToEntity(this AssertionLineageStep model) => new()
    {
        Id = model.Id,
        AssertionObservationId = model.AssertionObservationId,
        InputIndex = model.InputIndex,
        PathIndex = model.PathIndex,
        StepIndex = model.StepIndex,
        StepKind = model.StepKind.ToString(),
        Depth = model.Depth,
        SymbolDisplay = model.SymbolDisplay,
        MemberId = model.MemberId,
        FilePath = model.FilePath,
        StartLine = model.StartLine,
        StartColumn = model.StartColumn,
        EndLine = model.EndLine,
        EndColumn = model.EndColumn,
        Outcome = model.Outcome.ToString(),
        ReasonCode = model.ReasonCode,
        Summary = model.Summary
    };

    public static void CopyGeneratedIds(
        this AssertionLineageMeasurement model,
        AssertionLineageMeasurementEntity entity)
    {
        model.Id = entity.Id;
        for (var summaryIndex = 0; summaryIndex < model.TestSummaries.Count; summaryIndex++)
        {
            var summary = model.TestSummaries[summaryIndex];
            var summaryEntity = entity.GeneratedTestSummaries.ElementAt(summaryIndex);
            summary.Id = summaryEntity.Id;
            summary.AssertionLineageMeasurementId = entity.Id;
            for (var observationIndex = 0; observationIndex < summary.Observations.Count; observationIndex++)
            {
                var observation = summary.Observations[observationIndex];
                var observationEntity = summaryEntity.AssertionObservations.ElementAt(observationIndex);
                observation.Id = observationEntity.Id;
                observation.GeneratedTestAssertionSummaryId = summaryEntity.Id;
                for (var stepIndex = 0; stepIndex < observation.Steps.Count; stepIndex++)
                {
                    var step = observation.Steps[stepIndex];
                    var stepEntity = observationEntity.LineageSteps.ElementAt(stepIndex);
                    step.Id = stepEntity.Id;
                    step.AssertionObservationId = observationEntity.Id;
                }
            }
        }
    }

    private static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, false, out var result))
            return result;
        throw new InvalidOperationException($"Unknown persisted {typeof(TEnum).Name} value '{value}'.");
    }
}
