using TestMap.Models.AgentTools;
using TestMap.Persistence.Ef.Entities.AgentTools;
using TestMap.Persistence.Ef.Mapping.AgentTools;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="ToolAttemptMappingExtensions"/> covering round-trip fidelity,
/// enum parsing fallbacks, and StartedAt default handling.
/// </summary>
public sealed class ToolAttemptMappingExtensionsTests
{
    /// <summary>
    /// ToEntity followed by ToDomain round-trips all scalar fields without data loss.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntityThenToDomain_RoundTripsAllFields()
    {
        // Arrange
        var attempt = MakeFullAttempt();

        // Act
        var entity = attempt.ToEntity();
        var roundTripped = entity.ToDomain();

        // Assert
        Assert.Equal(attempt.Id, roundTripped.Id);
        Assert.Equal(attempt.ExperimentRunId, roundTripped.ExperimentRunId);
        Assert.Equal(attempt.MatrixWorkItemId, roundTripped.MatrixWorkItemId);
        Assert.Equal(attempt.CandidateMethodId, roundTripped.CandidateMethodId);
        Assert.Equal(attempt.TargetedBaselineId, roundTripped.TargetedBaselineId);
        Assert.Equal(attempt.EffectiveProfileHash, roundTripped.EffectiveProfileHash);
        Assert.Equal(attempt.ToolId, roundTripped.ToolId);
        Assert.Equal(attempt.RunStatus, roundTripped.RunStatus);
        Assert.Equal(attempt.ValidationOutcome, roundTripped.ValidationOutcome);
        Assert.Equal(attempt.ObservedOutcome, roundTripped.ObservedOutcome);
        Assert.Equal(attempt.ImageName, roundTripped.ImageName);
        Assert.Equal(attempt.ImageKey, roundTripped.ImageKey);
        Assert.Equal(attempt.BaseCommit, roundTripped.BaseCommit);
        Assert.Equal(attempt.WorkspacePath, roundTripped.WorkspacePath);
        Assert.Equal(attempt.ArtifactPath, roundTripped.ArtifactPath);
        Assert.Equal(attempt.StdOutLogPath, roundTripped.StdOutLogPath);
        Assert.Equal(attempt.StdErrLogPath, roundTripped.StdErrLogPath);
        Assert.Equal(attempt.JsonlLogPath, roundTripped.JsonlLogPath);
        Assert.Equal(attempt.StartedAt, roundTripped.StartedAt);
        Assert.Equal(attempt.CompletedAt, roundTripped.CompletedAt);
        Assert.Equal(attempt.ElapsedSeconds, roundTripped.ElapsedSeconds);
        Assert.Equal(attempt.GenerationDurationSeconds, roundTripped.GenerationDurationSeconds);
        Assert.Equal(attempt.ValidationDurationSeconds, roundTripped.ValidationDurationSeconds);
        Assert.Equal(attempt.TotalAttemptDurationSeconds, roundTripped.TotalAttemptDurationSeconds);
        Assert.Equal(attempt.TimeoutSeconds, roundTripped.TimeoutSeconds);
        Assert.Equal(attempt.ExitCode, roundTripped.ExitCode);
        Assert.Equal(attempt.ToolVersion, roundTripped.ToolVersion);
        Assert.Equal(attempt.Model, roundTripped.Model);
        Assert.Equal(attempt.ProviderId, roundTripped.ProviderId);
        Assert.Equal(attempt.JsonlLogAvailable, roundTripped.JsonlLogAvailable);
        Assert.Equal(attempt.UsageAvailable, roundTripped.UsageAvailable);
        Assert.Equal(attempt.UsageSource, roundTripped.UsageSource);
        Assert.Equal(attempt.InputTokens, roundTripped.InputTokens);
        Assert.Equal(attempt.OutputTokens, roundTripped.OutputTokens);
        Assert.Equal(attempt.EstimatedPromptTokens, roundTripped.EstimatedPromptTokens);
        Assert.Equal(attempt.ChangedFilesCount, roundTripped.ChangedFilesCount);
        Assert.Equal(attempt.ProductionFilesChanged, roundTripped.ProductionFilesChanged);
        Assert.Equal(attempt.TestFilesChanged, roundTripped.TestFilesChanged);
        Assert.Equal(attempt.ProjectFilesChanged, roundTripped.ProjectFilesChanged);
        Assert.Equal(attempt.DeletedFilesCount, roundTripped.DeletedFilesCount);
        Assert.Equal(attempt.ConstraintViolationSummary, roundTripped.ConstraintViolationSummary);
        Assert.Equal(attempt.Notes, roundTripped.Notes);
    }

    /// <summary>
    /// An unknown string in RunStatus falls back to ToolRunStatus.Planned rather than throwing.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomain_UnknownRunStatusString_FallsBackToPlanned()
    {
        // Arrange
        var entity = MakeMinimalEntity();
        entity.RunStatus = "UnknownFutureStatus";

        // Act
        var domain = entity.ToDomain();

        // Assert
        Assert.Equal(ToolRunStatus.Planned, domain.RunStatus);
    }

    /// <summary>
    /// An unknown string in ValidationOutcome falls back to ToolValidationOutcome.NotEvaluated.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomain_UnknownValidationOutcomeString_FallsBackToNotEvaluated()
    {
        // Arrange
        var entity = MakeMinimalEntity();
        entity.ValidationOutcome = "SomeUnknownOutcome";

        // Act
        var domain = entity.ToDomain();

        // Assert
        Assert.Equal(ToolValidationOutcome.NotEvaluated, domain.ValidationOutcome);
    }

    /// <summary>
    /// An unknown string in ObservedOutcome falls back to ToolObservedOutcome.NotEvaluated.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomain_UnknownObservedOutcomeString_FallsBackToNotEvaluated()
    {
        // Arrange
        var entity = MakeMinimalEntity();
        entity.ObservedOutcome = "SomeUnknownObserved";

        // Act
        var domain = entity.ToDomain();

        // Assert
        Assert.Equal(ToolObservedOutcome.NotEvaluated, domain.ObservedOutcome);
    }

    /// <summary>
    /// ToEntity rewrites a default (0 / unset) StartedAt to DateTime.UtcNow rather than
    /// persisting the sentinel epoch value.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_DefaultStartedAt_RewritesToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var attempt = new ToolAttempt { ExperimentRunId = 1, CandidateMethodId = 1, ToolId = "codex" };
        // StartedAt left as default

        // Act
        var entity = attempt.ToEntity();
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(entity.StartedAt, before, after);
    }

    /// <summary>
    /// ToEntity preserves an explicitly set StartedAt rather than overwriting it.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_ExplicitStartedAt_IsPreserved()
    {
        // Arrange
        var explicitTime = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var attempt = new ToolAttempt
        {
            ExperimentRunId = 1,
            CandidateMethodId = 1,
            ToolId = "codex",
            StartedAt = explicitTime
        };

        // Act
        var entity = attempt.ToEntity();

        // Assert
        Assert.Equal(explicitTime, entity.StartedAt);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ToolAttempt MakeFullAttempt() => new()
    {
        Id = 42,
        ExperimentRunId = 10,
        MatrixWorkItemId = 5,
        CandidateMethodId = 7,
        TargetedBaselineId = 3,
        EffectiveProfileHash = "abc123",
        ToolId = "codex",
        RunStatus = ToolRunStatus.Completed,
        ValidationOutcome = ToolValidationOutcome.Passed,
        ObservedOutcome = ToolObservedOutcome.ValidatedLowImpact,
        ImageName = "testmap-agent-eval-codex:latest",
        ImageKey = "codex",
        BaseCommit = "deadbeef",
        WorkspacePath = "/workspaces/repo",
        ArtifactPath = "/output/attempts/42",
        StdOutLogPath = "/output/attempts/42/codex.events.jsonl",
        StdErrLogPath = "/output/attempts/42/codex.stderr.log",
        JsonlLogPath = "/output/attempts/42/codex.events.jsonl",
        StartedAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
        CompletedAt = new DateTime(2026, 6, 1, 12, 30, 0, DateTimeKind.Utc),
        ElapsedSeconds = 1800.5,
        GenerationDurationSeconds = 1800.5,
        ValidationDurationSeconds = 45.25,
        TotalAttemptDurationSeconds = 1845.75,
        TimeoutSeconds = 2700,
        ExitCode = 0,
        ToolVersion = "0.1.2",
        Model = "gpt-4o",
        ProviderId = "openai",
        JsonlLogAvailable = true,
        UsageAvailable = true,
        UsageSource = "tool-jsonl",
        InputTokens = 5000,
        OutputTokens = 2000,
        EstimatedPromptTokens = 4800,
        ChangedFilesCount = 3,
        ProductionFilesChanged = 0,
        TestFilesChanged = 2,
        ProjectFilesChanged = 1,
        DeletedFilesCount = 0,
        ConstraintViolationSummary = string.Empty,
        Notes = "all good"
    };

    private static ToolAttemptEntity MakeMinimalEntity() => new()
    {
        Id = 1,
        ExperimentRunId = 1,
        CandidateMethodId = 1,
        EffectiveProfileHash = string.Empty,
        ToolId = "codex",
        RunStatus = "Planned",
        ValidationOutcome = "NotEvaluated",
        ObservedOutcome = "NotEvaluated",
        ImageName = string.Empty,
        ImageKey = string.Empty,
        BaseCommit = string.Empty,
        WorkspacePath = string.Empty,
        ArtifactPath = string.Empty,
        StdOutLogPath = string.Empty,
        StdErrLogPath = string.Empty,
        JsonlLogPath = string.Empty,
        StartedAt = DateTime.UtcNow,
        ToolVersion = string.Empty,
        Model = string.Empty,
        ProviderId = string.Empty,
        UsageSource = string.Empty,
        ConstraintViolationSummary = string.Empty,
        Notes = string.Empty
    };
}
