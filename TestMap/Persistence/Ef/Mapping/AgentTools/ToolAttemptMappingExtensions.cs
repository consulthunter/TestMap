using TestMap.Models.AgentTools;
using TestMap.Persistence.Ef.Entities.AgentTools;

namespace TestMap.Persistence.Ef.Mapping.AgentTools;

public static class ToolAttemptMappingExtensions
{
    public static ToolAttempt ToDomain(this ToolAttemptEntity entity) => new()
    {
        Id = entity.Id,
        ExperimentRunId = entity.ExperimentRunId,
        MatrixWorkItemId = entity.MatrixWorkItemId,
        CandidateMethodId = entity.CandidateMethodId,
        TargetedBaselineId = entity.TargetedBaselineId,
        PostAttemptTestRunId = entity.PostAttemptTestRunId,
        EffectiveProfileHash = entity.EffectiveProfileHash,
        ToolId = entity.ToolId,
        RunStatus = ParseEnum(entity.RunStatus, ToolRunStatus.Planned),
        ValidationOutcome = ParseEnum(entity.ValidationOutcome, ToolValidationOutcome.NotEvaluated),
        ObservedOutcome = ParseEnum(entity.ObservedOutcome, ToolObservedOutcome.NotEvaluated),
        ImageName = entity.ImageName,
        ImageKey = entity.ImageKey,
        BaseCommit = entity.BaseCommit,
        WorkspacePath = entity.WorkspacePath,
        ArtifactPath = entity.ArtifactPath,
        StartedAt = entity.StartedAt,
        CompletedAt = entity.CompletedAt,
        ElapsedSeconds = entity.ElapsedSeconds,
        TimeoutSeconds = entity.TimeoutSeconds,
        ExitCode = entity.ExitCode,
        ToolVersion = entity.ToolVersion,
        Model = entity.Model,
        ProviderId = entity.ProviderId,
        JsonlLogAvailable = entity.JsonlLogAvailable,
        UsageAvailable = entity.UsageAvailable,
        UsageSource = entity.UsageSource,
        InputTokens = entity.InputTokens,
        OutputTokens = entity.OutputTokens,
        EstimatedPromptTokens = entity.EstimatedPromptTokens,
        ChangedFilesCount = entity.ChangedFilesCount,
        ProductionFilesChanged = entity.ProductionFilesChanged,
        TestFilesChanged = entity.TestFilesChanged,
        ProjectFilesChanged = entity.ProjectFilesChanged,
        DeletedFilesCount = entity.DeletedFilesCount,
        ConstraintViolationSummary = entity.ConstraintViolationSummary,
        Notes = entity.Notes
    };

    public static ToolAttemptEntity ToEntity(this ToolAttempt attempt) => new()
    {
        Id = attempt.Id,
        ExperimentRunId = attempt.ExperimentRunId,
        MatrixWorkItemId = attempt.MatrixWorkItemId,
        CandidateMethodId = attempt.CandidateMethodId,
        TargetedBaselineId = attempt.TargetedBaselineId,
        PostAttemptTestRunId = attempt.PostAttemptTestRunId,
        EffectiveProfileHash = attempt.EffectiveProfileHash,
        ToolId = attempt.ToolId,
        RunStatus = attempt.RunStatus.ToString(),
        ValidationOutcome = attempt.ValidationOutcome.ToString(),
        ObservedOutcome = attempt.ObservedOutcome.ToString(),
        ImageName = attempt.ImageName,
        ImageKey = attempt.ImageKey,
        BaseCommit = attempt.BaseCommit,
        WorkspacePath = attempt.WorkspacePath,
        ArtifactPath = attempt.ArtifactPath,
        StartedAt = attempt.StartedAt == default ? DateTime.UtcNow : attempt.StartedAt,
        CompletedAt = attempt.CompletedAt,
        ElapsedSeconds = attempt.ElapsedSeconds,
        TimeoutSeconds = attempt.TimeoutSeconds,
        ExitCode = attempt.ExitCode,
        ToolVersion = attempt.ToolVersion,
        Model = attempt.Model,
        ProviderId = attempt.ProviderId,
        JsonlLogAvailable = attempt.JsonlLogAvailable,
        UsageAvailable = attempt.UsageAvailable,
        UsageSource = attempt.UsageSource,
        InputTokens = attempt.InputTokens,
        OutputTokens = attempt.OutputTokens,
        EstimatedPromptTokens = attempt.EstimatedPromptTokens,
        ChangedFilesCount = attempt.ChangedFilesCount,
        ProductionFilesChanged = attempt.ProductionFilesChanged,
        TestFilesChanged = attempt.TestFilesChanged,
        ProjectFilesChanged = attempt.ProjectFilesChanged,
        DeletedFilesCount = attempt.DeletedFilesCount,
        ConstraintViolationSummary = attempt.ConstraintViolationSummary,
        Notes = attempt.Notes
    };

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var result) ? result : fallback;
}
