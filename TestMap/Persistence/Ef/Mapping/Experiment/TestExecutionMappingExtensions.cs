using System.Text.Json;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Mapping.Experiment;

public static class TestExecutionMappingExtensions
{
    public static TestExecution ToDomain(this TestExecutionEntity entity)
    {
        var structured = ParseStructuredErrors(entity.StructuredErrors);
        var classification =
            Enum.TryParse<TestClassification>(entity.TestClassification, true, out var parsedClassification)
                ? parsedClassification
                : TestClassification.Failed;
        var failureKind = structured?.FailureKind is { } kindText &&
                          Enum.TryParse<TestFailureKind>(kindText, true, out var parsedFailureKind)
            ? parsedFailureKind
            : InferFailureKind(entity);

        return new TestExecution
        {
            Id = entity.Id,
            GenerationAttemptId = entity.GenerationAttemptId,
            GeneratedTestCode = entity.GeneratedTestCode,
            GeneratedTestMethodName = entity.GeneratedTestMethodName,
            CompilationSuccess = entity.CompilationSucceeded,
            TestPassed = entity.TestPassed,
            CoverageAfter = entity.FinalCoverage,
            CoverageImprovement = entity.CoverageDelta,
            BaselineMutationScore = entity.BaselineMutationScore,
            MutationScoreAfter = entity.MutationScoreAfter,
            MutationScoreImprovement = entity.MutationScoreDelta,
            Classification = classification,
            FailureKind = failureKind,
            CompilationErrors = EmptyToNull(entity.CompilationErrors),
            RuntimeErrors = EmptyToNull(entity.RuntimeErrors),
            AssertionErrors = EmptyToNull(entity.AssertionErrors),
            FailureStage = structured?.Stage,
            FailureCategory = structured?.Category,
            FailureSummary = structured?.Summary,
            StructuredErrors = EmptyToNull(entity.StructuredErrors),
            ErrorLogs = string.Join(
                "\n",
                new[] { entity.CompilationErrors, entity.RuntimeErrors, entity.AssertionErrors }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
            ExecutionTimeMs = entity.ExecutionTimeMs,
            ExecutedAt = entity.ExecutionTime
        };
    }

    public static TestExecutionEntity ToEntity(this TestExecution execution)
    {
        var classification = ResolveClassification(execution);
        var compilationErrors = execution.CompilationErrors;
        var runtimeErrors = execution.RuntimeErrors;
        var assertionErrors = execution.AssertionErrors;

        if (string.IsNullOrWhiteSpace(compilationErrors) &&
            string.IsNullOrWhiteSpace(runtimeErrors) &&
            string.IsNullOrWhiteSpace(assertionErrors) &&
            !string.IsNullOrWhiteSpace(execution.ErrorLogs))
            switch (execution.FailureKind)
            {
                case TestFailureKind.Compilation:
                    compilationErrors = execution.ErrorLogs;
                    break;
                case TestFailureKind.Assertion:
                    assertionErrors = execution.ErrorLogs;
                    break;
                case TestFailureKind.Runtime:
                case TestFailureKind.Infrastructure:
                case TestFailureKind.Generation:
                case TestFailureKind.Unknown:
                    runtimeErrors = execution.ErrorLogs;
                    break;
            }

        return new TestExecutionEntity
        {
            Id = execution.Id,
            GenerationAttemptId = execution.GenerationAttemptId,
            GeneratedTestCode = execution.GeneratedTestCode ?? string.Empty,
            GeneratedTestMethodName = execution.GeneratedTestMethodName ?? string.Empty,
            CompilationSucceeded = execution.CompilationSuccess,
            CompilationErrors = compilationErrors ?? string.Empty,
            TestPassed = execution.TestPassed,
            RuntimeErrors = runtimeErrors ?? string.Empty,
            AssertionErrors = assertionErrors ?? string.Empty,
            ExecutionTimeMs = execution.ExecutionTimeMs ?? 0,
            FinalCoverage = execution.CoverageAfter,
            FinalCoveredLines = 0,
            FinalTotalLines = 0,
            CoverageDelta = execution.CoverageImprovement,
            BaselineMutationScore = execution.BaselineMutationScore,
            MutationScoreAfter = execution.MutationScoreAfter,
            MutationScoreDelta = execution.MutationScoreImprovement,
            NewLinesCovered = 0,
            TestClassification = classification.ToString(),
            ExecutionTime = execution.ExecutedAt == default ? DateTime.UtcNow : execution.ExecutedAt,
            StructuredErrors = SerializeStructuredErrors(execution)
        };
    }

    private static TestClassification ResolveClassification(TestExecution execution)
    {
        if (execution.TestPassed)
            return execution.CoverageImprovement > 0 ? TestClassification.Approved : TestClassification.Benign;

        return execution.CoverageImprovement > 0 ? TestClassification.Candidate : TestClassification.Failed;
    }

    private static TestFailureKind InferFailureKind(TestExecutionEntity entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.CompilationErrors)) return TestFailureKind.Compilation;

        if (!string.IsNullOrWhiteSpace(entity.AssertionErrors)) return TestFailureKind.Assertion;

        if (!string.IsNullOrWhiteSpace(entity.RuntimeErrors)) return TestFailureKind.Runtime;

        return entity.TestPassed ? TestFailureKind.None : TestFailureKind.Unknown;
    }

    private static string SerializeStructuredErrors(TestExecution execution)
    {
        if (string.IsNullOrWhiteSpace(execution.FailureStage) &&
            string.IsNullOrWhiteSpace(execution.FailureCategory) &&
            string.IsNullOrWhiteSpace(execution.FailureSummary) &&
            execution.FailureKind == TestFailureKind.None)
            return execution.StructuredErrors ?? string.Empty;

        return JsonSerializer.Serialize(new StructuredFailurePayload(
            execution.FailureKind.ToString(),
            execution.FailureStage,
            execution.FailureCategory,
            execution.FailureSummary));
    }

    private static StructuredFailurePayload? ParseStructuredErrors(string? structuredErrors)
    {
        if (string.IsNullOrWhiteSpace(structuredErrors)) return null;

        try
        {
            return JsonSerializer.Deserialize<StructuredFailurePayload>(structuredErrors);
        }
        catch
        {
            return null;
        }
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record StructuredFailurePayload(
        string FailureKind,
        string? Stage,
        string? Category,
        string? Summary);
}