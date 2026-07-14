using TestMap.Models.AgentTools;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Repositories.AgentTools;
using TestMap.Services.TestExecution;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.Experiment.Execution;

public sealed class ToolPostAttemptMeasurementResult
{
    /// <summary>Whether a build/test measurement was actually performed.</summary>
    public bool Measured { get; init; }

    /// <summary>
    /// Reason the measurement was skipped. Empty when <see cref="Measured"/> is true.
    /// </summary>
    public string SkipReason { get; init; } = string.Empty;

    /// <summary>The persisted test run id from the measurement, when available.</summary>
    public int? TestRunId { get; init; }

    /// <summary>Reclassified validation outcome based on measurement results.</summary>
    public ToolValidationOutcome ValidationOutcome { get; init; } = ToolValidationOutcome.NotEvaluated;

    /// <summary>Reclassified observed outcome based on measurement results.</summary>
    public ToolObservedOutcome ObservedOutcome { get; init; } = ToolObservedOutcome.NotEvaluated;
}

public interface IToolPostAttemptMeasurementService
{
    /// <summary>
    /// Runs build/test on the modified workspace after a completed tool attempt, then
    /// reclassifies <see cref="ToolValidationOutcome"/> and <see cref="ToolObservedOutcome"/>.
    /// Updates the attempt in the database with the new outcomes and test run id.
    /// </summary>
    Task<ToolPostAttemptMeasurementResult> MeasureAsync(
        ToolAttempt attempt,
        CandidateMethod candidate,
        CandidateMethodContext methodContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs the build/test pipeline on the tool-modified workspace and reclassifies the
/// <see cref="ToolAttempt"/> outcome from <c>ChangedNotValidated</c> to one of the
/// validated outcomes.
/// </summary>
public sealed class ToolPostAttemptMeasurementService : IToolPostAttemptMeasurementService
{
    private readonly IBuildTestService _buildTestService;
    private readonly ToolAttemptRepository _attemptRepo;
    private readonly IAttemptMetricComparisonService _metricComparisonService;

    public ToolPostAttemptMeasurementService(
        IBuildTestService buildTestService,
        ToolAttemptRepository attemptRepo,
        IAttemptMetricComparisonService metricComparisonService)
    {
        _buildTestService = buildTestService;
        _attemptRepo = attemptRepo;
        _metricComparisonService = metricComparisonService;
    }

    public async Task<ToolPostAttemptMeasurementResult> MeasureAsync(
        ToolAttempt attempt,
        CandidateMethod candidate,
        CandidateMethodContext methodContext,
        CancellationToken cancellationToken = default)
    {
        // Only measure when the tool produced changes. Other statuses keep their
        // provisional outcome (Skipped, TimedOut, ToolFailed, NoChange).
        if (attempt.RunStatus != ToolRunStatus.Completed)
            return Skip("Measurement only runs for Completed tool attempts with changes.");

        if (string.IsNullOrWhiteSpace(methodContext.TestProjectPath))
            return Skip("Candidate has no resolved test project path.");

        if (string.IsNullOrWhiteSpace(methodContext.SourceProjectPath))
            return Skip("Candidate has no resolved source project path.");

        var run = await _buildTestService.BuildTestAsync(
            BuildTestRunRequest.CreateIteration(
                methodContext.TestProjectPath,
                methodContext.TargetBuildFramework,
                methodContext.ToTargetMemberDescriptor(candidate),
                methodContext.SourceProjectPath,
                attempt.ExperimentRunId,
                isMutationBaseline: false));

        int? testRunId = run.DbId > 0 ? run.DbId : null;
        var comparison = await _metricComparisonService.CompareAsync(
            candidate.MemberId,
            attempt.TargetedBaselineId,
            testRunId,
            cancellationToken);
        var (validationOutcome, observedOutcome) = Classify(run, comparison);

        attempt.PostAttemptTestRunId = testRunId;
        attempt.CoverageBefore = comparison.CoverageBefore;
        attempt.CoverageAfter = comparison.CoverageAfter;
        attempt.CoverageDelta = comparison.CoverageDelta;
        attempt.CoverageMeasurementStatus = comparison.CoverageStatus;
        attempt.MutationScoreBefore = comparison.MutationScoreBefore;
        attempt.MutationScoreAfter = comparison.MutationScoreAfter;
        attempt.MutationScoreDelta = comparison.MutationScoreDelta;
        attempt.MutationMeasurementStatus = comparison.MutationStatus;
        attempt.ImpactMeasurementStatus = comparison.ImpactStatus;
        attempt.MeasurementFailureReason = comparison.FailureReason;
        attempt.MeasurementPolicyVersion = EvaluationImpactPolicy.Version;
        attempt.ValidationOutcome = validationOutcome;
        attempt.ObservedOutcome = observedOutcome;
        await _attemptRepo.UpdateAsync(attempt, cancellationToken);

        return new ToolPostAttemptMeasurementResult
        {
            Measured = true,
            TestRunId = testRunId,
            ValidationOutcome = validationOutcome,
            ObservedOutcome = observedOutcome
        };
    }

    /// <summary>
    /// Classifies the measurement result into validation and observed outcomes.
    /// </summary>
    internal static (ToolValidationOutcome Validation, ToolObservedOutcome Observed)
        Classify(
            TestMap.Models.Testing.TestRunModel run,
            AttemptMetricComparison comparison)
    {
        var impact = EvaluationImpactPolicy.Evaluate(
            run.Success,
            comparison.CoverageDelta,
            comparison.MutationScoreDelta);

        if (!run.Success)
        {
            // Tests failed or build failed — determine which.
            var validationOutcome = run.Results.Any(r =>
                    !string.Equals(r.Outcome, "Passed", StringComparison.OrdinalIgnoreCase))
                ? ToolValidationOutcome.TestsFailed
                : ToolValidationOutcome.BuildFailed;
            return (validationOutcome, impact.MetricImproved
                ? ToolObservedOutcome.FailedEvidencePositive
                : ToolObservedOutcome.ValidationFailed);
        }

        if (impact.ValidatedEvidencePositive)
            return (ToolValidationOutcome.Passed, ToolObservedOutcome.ValidatedEvidencePositive);
        if (impact.ValidatedLowImpact)
            return (ToolValidationOutcome.Passed, ToolObservedOutcome.ValidatedLowImpact);
        return (ToolValidationOutcome.Passed, ToolObservedOutcome.ValidatedImpactUnknown);
    }

    private static ToolPostAttemptMeasurementResult Skip(string reason) =>
        new() { Measured = false, SkipReason = reason };
}
