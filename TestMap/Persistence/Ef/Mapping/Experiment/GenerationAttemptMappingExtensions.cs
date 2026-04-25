using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Mapping.Experiment;

public static class GenerationAttemptMappingExtensions
{
    public static GenerationAttempt ToDomain(this GenerationAttemptEntity entity)
    {
        return new GenerationAttempt
        {
            Id = entity.Id,
            CandidateMethodId = entity.CandidateMethodId,
            Provider = Enum.TryParse<AiProvider>(entity.ProviderName, true, out var provider)
                ? provider
                : AiProvider.OpenAi,
            ModelName = entity.ModelName,
            Strategy = Enum.TryParse<GenerationStrategy>(entity.Strategy, true, out var strategy)
                ? strategy
                : GenerationStrategy.Pass1,
            AttemptNumber = entity.AttemptNumber,
            StartedAt = entity.StartTime,
            CompletedAt = entity.EndTime,
            TotalTokensUsed = entity.TotalTokensUsed,
            TotalDurationSeconds = entity.EndTime.HasValue ? (entity.EndTime.Value - entity.StartTime).TotalSeconds : 0,
            Status = entity.Status,
            FailureKind = Enum.TryParse<TestFailureKind>(entity.FailureKind, true, out var failureKind)
                ? failureKind
                : TestFailureKind.None,
            FailureStage = EmptyToNull(entity.FailureStage),
            FailureCategory = EmptyToNull(entity.FailureCategory),
            ErrorMessage = EmptyToNull(entity.ErrorMessage) ?? ResolveErrorMessage(entity),
            GenerationSteps = entity.GenerationSteps?.Select(x => x.ToDomain()).ToList() ?? new List<GenerationStep>(),
            TestExecution = entity.TestExecution?.ToDomain()
        };
    }

    public static GenerationAttemptEntity ToEntity(this GenerationAttempt attempt)
    {
        return new GenerationAttemptEntity
        {
            Id = attempt.Id,
            CandidateMethodId = attempt.CandidateMethodId,
            ProviderName = attempt.Provider.ToString(),
            ModelName = attempt.ModelName ?? string.Empty,
            Strategy = attempt.Strategy.ToString(),
            AttemptNumber = attempt.AttemptNumber,
            IsRepairAttempt = attempt.Strategy == GenerationStrategy.Repair5 && attempt.AttemptNumber > 1,
            StartTime = attempt.StartedAt == default ? DateTime.UtcNow : attempt.StartedAt,
            EndTime = attempt.CompletedAt,
            TotalTokensUsed = attempt.TotalTokensUsed,
            Status = ResolveStatus(attempt),
            FailureKind = ResolveFailureKind(attempt).ToString(),
            FailureStage = ResolveFailureStage(attempt) ?? string.Empty,
            FailureCategory = ResolveFailureCategory(attempt) ?? string.Empty,
            ErrorMessage = ResolvePersistedErrorMessage(attempt) ?? string.Empty
        };
    }

    private static string? ResolveErrorMessage(GenerationAttemptEntity entity)
    {
        if (entity.TestExecution != null)
        {
            var execution = entity.TestExecution.ToDomain();
            if (!string.IsNullOrWhiteSpace(execution.ErrorLogs)) return execution.ErrorLogs;
        }

        return entity.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            ? null
            : entity.Status;
    }

    private static string ResolveStatus(GenerationAttempt attempt)
    {
        if (attempt.TestExecution != null)
            return attempt.TestExecution.FailureKind switch
            {
                TestFailureKind.None => "Completed",
                TestFailureKind.Generation => "GenerationFailed",
                TestFailureKind.Compilation => "CompilationFailed",
                TestFailureKind.Runtime => "RuntimeFailed",
                TestFailureKind.Assertion => "AssertionFailed",
                TestFailureKind.Infrastructure => "InfrastructureFailed",
                _ => "Failed"
            };

        return string.IsNullOrWhiteSpace(attempt.ErrorMessage) ? "Completed" : "Failed";
    }

    private static TestFailureKind ResolveFailureKind(GenerationAttempt attempt)
    {
        if (attempt.FailureKind != TestFailureKind.None) return attempt.FailureKind;

        return attempt.TestExecution?.FailureKind ?? (string.IsNullOrWhiteSpace(attempt.ErrorMessage)
            ? TestFailureKind.None
            : TestFailureKind.Unknown);
    }

    private static string? ResolveFailureStage(GenerationAttempt attempt)
    {
        return attempt.FailureStage
               ?? attempt.TestExecution?.FailureStage;
    }

    private static string? ResolveFailureCategory(GenerationAttempt attempt)
    {
        return attempt.FailureCategory
               ?? attempt.TestExecution?.FailureCategory;
    }

    private static string? ResolvePersistedErrorMessage(GenerationAttempt attempt)
    {
        return attempt.ErrorMessage
               ?? attempt.TestExecution?.ErrorLogs;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}