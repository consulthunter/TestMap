using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TestMap.Models.Experiment.Assertions;
using TestMap.Persistence.Ef.Entities.Experiment.Assertions;
using TestMap.Persistence.Ef.Mapping.Experiment.Assertions;
using TestMap.Services.Experiment.Reporting;

namespace TestMap.Persistence.Ef.Repositories.Experiment.Assertions;

/// <param name="AuditFindings">
/// Null when the measurement was stored as produced; otherwise the audit findings that caused
/// an Unavailable measurement to be stored in its place.
/// </param>
public sealed record AssertionLineageInsertResult(int Id, string? AuditFindings)
{
    public bool Degraded => AuditFindings != null;
}

public sealed class AssertionLineageMeasurementRepository
{
    private readonly TestMapDbContext _context;
    private readonly IAssertionLineageSummaryService _summaryService;
    private readonly IAssertionLineageAuditService _auditService;

    public AssertionLineageMeasurementRepository(
        TestMapDbContext context,
        IAssertionLineageSummaryService summaryService,
        IAssertionLineageAuditService auditService)
    {
        _context = context;
        _summaryService = summaryService;
        _auditService = auditService;
    }

    /// <summary>
    /// Inserts a measurement, substituting an Unavailable one when the integrity audit rejects
    /// the evidence. Assertion lineage enriches a result rather than deciding whether it is
    /// valid, so rejected evidence must not abort the attempt that produced it: the caller
    /// keeps its coverage, mutation and outcome data, and the stored row states plainly that
    /// the enrichment did not survive. Returns the audit findings when degraded so the caller
    /// can report where it happened.
    /// </summary>
    public async Task<AssertionLineageInsertResult> InsertOrDegradeAsync(
        AssertionLineageMeasurement measurement,
        CancellationToken cancellationToken = default)
    {
        _summaryService.ApplyAttemptSummary(measurement);
        var audit = _auditService.ValidateMeasurement(measurement);
        if (audit.Passed)
            return new AssertionLineageInsertResult(
                await InsertValidatedAsync(measurement, cancellationToken),
                null);

        var findings = string.Join("; ", audit.Findings.Select(x => $"{x.Code}: {x.Message}"));
        var degraded = new AssertionLineageMeasurement
        {
            ProjectId = measurement.ProjectId,
            ExperimentRunId = measurement.ExperimentRunId,
            CandidateMethodId = measurement.CandidateMethodId,
            GenerationAttemptId = measurement.GenerationAttemptId,
            ToolAttemptId = measurement.ToolAttemptId,
            ProducerLane = measurement.ProducerLane,
            Status = AssertionMeasurementStatus.Unavailable,
            FailureCode = AssertionLineageReasonCodes.AssertionAuditFailed,
            FailureReason =
                "Assertion-lineage evidence was rejected by the integrity audit: " + findings,
            PolicyVersion = measurement.PolicyVersion,
            AssertionCatalogVersion = measurement.AssertionCatalogVersion,
            MaxDepth = measurement.MaxDepth,
            AnalysisDurationMs = measurement.AnalysisDurationMs,
            StartedAt = measurement.StartedAt,
            CompletedAt = measurement.CompletedAt
        };

        _summaryService.ApplyAttemptSummary(degraded);
        // The substitute carries no observations, so a rejection here would mean the degraded
        // shape itself is malformed — a defect worth surfacing rather than swallowing.
        _auditService.ValidateMeasurement(degraded).ThrowIfFailed();
        return new AssertionLineageInsertResult(
            await InsertValidatedAsync(degraded, cancellationToken),
            findings);
    }

    public async Task<int> InsertAsync(
        AssertionLineageMeasurement measurement,
        CancellationToken cancellationToken = default)
    {
        _summaryService.ApplyAttemptSummary(measurement);
        _auditService.ValidateMeasurement(measurement).ThrowIfFailed();
        return await InsertValidatedAsync(measurement, cancellationToken);
    }

    private async Task<int> InsertValidatedAsync(
        AssertionLineageMeasurement measurement,
        CancellationToken cancellationToken)
    {

        var duplicateExists = await _context.AssertionLineageMeasurements.AnyAsync(
            x => x.PolicyVersion == measurement.PolicyVersion &&
                 x.AssertionCatalogVersion == measurement.AssertionCatalogVersion &&
                 x.MaxDepth == measurement.MaxDepth &&
                 (measurement.GenerationAttemptId.HasValue
                     ? x.GenerationAttemptId == measurement.GenerationAttemptId
                     : x.ToolAttemptId == measurement.ToolAttemptId),
            cancellationToken);
        if (duplicateExists)
            throw new InvalidOperationException(
                "Assertion-lineage measurements are immutable; an observation already exists for this attempt and policy.");

        IDbContextTransaction? transaction = null;
        if (_context.Database.CurrentTransaction == null)
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var entity = measurement.ToEntity();
            _context.AssertionLineageMeasurements.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);
            measurement.CopyGeneratedIds(entity);
            return entity.Id;
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<AssertionLineageMeasurement?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await CompleteGraph()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<AssertionLineageMeasurement>> GetByExperimentRunIdAsync(
        int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        var entities = await CompleteGraph()
            .Where(x => x.ExperimentRunId == experimentRunId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public Task<AssertionLineageMeasurement> GetForGenerationAttemptOrNotMeasuredAsync(
        int generationAttemptId,
        string policyVersion,
        string catalogVersion,
        int maxDepth,
        CancellationToken cancellationToken = default) =>
        GetForAttemptOrNotMeasuredAsync(
            generationAttemptId,
            true,
            policyVersion,
            catalogVersion,
            maxDepth,
            cancellationToken);

    public Task<AssertionLineageMeasurement> GetForToolAttemptOrNotMeasuredAsync(
        int toolAttemptId,
        string policyVersion,
        string catalogVersion,
        int maxDepth,
        CancellationToken cancellationToken = default) =>
        GetForAttemptOrNotMeasuredAsync(
            toolAttemptId,
            false,
            policyVersion,
            catalogVersion,
            maxDepth,
            cancellationToken);

    private async Task<AssertionLineageMeasurement> GetForAttemptOrNotMeasuredAsync(
        int attemptId,
        bool generationLane,
        string policyVersion,
        string catalogVersion,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        var existing = await CompleteGraph()
            .FirstOrDefaultAsync(
                x => (generationLane
                         ? x.GenerationAttemptId == attemptId
                         : x.ToolAttemptId == attemptId) &&
                     x.PolicyVersion == policyVersion &&
                     x.AssertionCatalogVersion == catalogVersion &&
                     x.MaxDepth == maxDepth,
                cancellationToken);
        if (existing != null)
            return existing.ToDomain();

        if (generationLane)
        {
            var owner = await _context.GenerationAttempts
                .AsNoTracking()
                .Where(x => x.Id == attemptId)
                .Select(x => new
                {
                    x.CandidateMethodId,
                    ExperimentRunId = x.CandidateMethod!.ExperimentRunId,
                    ProjectId = x.CandidateMethod.ExperimentRun!.ProjectId,
                    x.StartTime,
                    x.EndTime
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException($"Generation attempt '{attemptId}' was not found.");
            return Historical(
                owner.ProjectId,
                owner.ExperimentRunId,
                owner.CandidateMethodId,
                attemptId,
                null,
                "testmap",
                policyVersion,
                catalogVersion,
                maxDepth,
                owner.StartTime,
                owner.EndTime ?? owner.StartTime);
        }

        var toolOwner = await _context.ToolAttempts
            .AsNoTracking()
            .Where(x => x.Id == attemptId)
            .Select(x => new
            {
                x.ExperimentRunId,
                x.CandidateMethodId,
                ProjectId = x.ExperimentRun!.ProjectId,
                x.StartedAt,
                x.CompletedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Tool attempt '{attemptId}' was not found.");
        return Historical(
            toolOwner.ProjectId,
            toolOwner.ExperimentRunId,
            toolOwner.CandidateMethodId,
            null,
            attemptId,
            "agent-tool",
            policyVersion,
            catalogVersion,
            maxDepth,
            toolOwner.StartedAt,
            toolOwner.CompletedAt ?? toolOwner.StartedAt);
    }

    private IQueryable<AssertionLineageMeasurementEntity> CompleteGraph() =>
        _context.AssertionLineageMeasurements
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.GeneratedTestSummaries)
            .ThenInclude(x => x.AssertionObservations)
            .ThenInclude(x => x.LineageSteps);

    private static AssertionLineageMeasurement Historical(
        int projectId,
        int experimentRunId,
        int candidateMethodId,
        int? generationAttemptId,
        int? toolAttemptId,
        string producerLane,
        string policyVersion,
        string catalogVersion,
        int maxDepth,
        DateTime startedAt,
        DateTime completedAt) =>
        new()
        {
            ProjectId = projectId,
            ExperimentRunId = experimentRunId,
            CandidateMethodId = candidateMethodId,
            GenerationAttemptId = generationAttemptId,
            ToolAttemptId = toolAttemptId,
            ProducerLane = producerLane,
            Status = AssertionMeasurementStatus.NotMeasured,
            FailureCode = AssertionLineageReasonCodes.HistoricalNotMeasured,
            FailureReason = "This attempt predates assertion-lineage measurement.",
            PolicyVersion = policyVersion,
            AssertionCatalogVersion = catalogVersion,
            MaxDepth = maxDepth,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };
}
