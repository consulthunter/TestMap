using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Code;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Services.StaticAnalysis;

public interface ISourceTestMappingRefreshService
{
    /// <summary>
    /// Traces the tests an analysis pass observed and upserts their mappings. A baseline pass
    /// traces every observed test; an attempt pass traces only the tests the attempt added or
    /// changed, which are the observed test rows that are not baseline rows. Rows for tests that
    /// were not traced are left untouched, so mapping ids stay stable across passes.
    /// </summary>
    Task RefreshAsync(
        int projectId,
        IEnumerable<int> solutionIds,
        IReadOnlySet<int> observedMemberIds,
        MemberAnalysisOrigin origin,
        CancellationToken cancellationToken = default);
}

public class SourceTestMappingRefreshService : ISourceTestMappingRefreshService
{
    private readonly ProjectContext _context;
    private readonly TestMapDbContext _dbContext;
    private readonly IRoslynSourceTestTraceService _roslynTraceService;

    public SourceTestMappingRefreshService(
        ProjectContext context,
        TestMapDbContext dbContext,
        IRoslynSourceTestTraceService roslynTraceService)
    {
        _context = context;
        _dbContext = dbContext;
        _roslynTraceService = roslynTraceService;
    }

    public async Task RefreshAsync(
        int projectId,
        IEnumerable<int> solutionIds,
        IReadOnlySet<int> observedMemberIds,
        MemberAnalysisOrigin origin,
        CancellationToken cancellationToken = default)
    {
        var testMemberIds = await ResolveTracedTestMemberIdsAsync(observedMemberIds, origin, cancellationToken);
        if (testMemberIds.Count == 0)
        {
            _context.Logger?.Information(
                "No {Origin} test members to trace for project {ProjectId}; source-test mappings unchanged.",
                origin.Kind,
                projectId);
            return;
        }

        var mappings = new List<SourceTestMappingEntity>();
        foreach (var solutionId in solutionIds.Distinct())
            mappings.AddRange(await _roslynTraceService.TraceAsync(
                projectId,
                solutionId,
                observedMemberIds,
                testMemberIds,
                cancellationToken) ?? []);

        var (updated, inserted, removed) = await UpsertMappingsAsync(
            projectId,
            testMemberIds,
            mappings,
            cancellationToken);
        _context.Logger?.Information(
            "Refreshed source-test mappings for {TestCount} {Origin} test member(s) in project {ProjectId} using roslyn-source-test-trace-v1: {Updated} updated, {Inserted} inserted, {Removed} removed.",
            testMemberIds.Count,
            origin.Kind,
            projectId,
            updated,
            inserted,
            removed);
    }

    private async Task<HashSet<int>> ResolveTracedTestMemberIdsAsync(
        IReadOnlySet<int> observedMemberIds,
        MemberAnalysisOrigin origin,
        CancellationToken cancellationToken)
    {
        if (observedMemberIds.Count == 0) return [];

        var testMembers = await _dbContext.Members
            .AsNoTracking()
            .Where(x => x.IsTestMember && x.Kind == "method")
            .Select(x => new { x.Id, x.OriginKind })
            .ToListAsync(cancellationToken);

        // An unchanged test reuses its baseline row during an attempt pass, so its baseline
        // mapping already describes it. Only rows the attempt versioned need tracing.
        var tracesBaseline = origin.Kind == MemberOrigin.Baseline;
        return testMembers
            .Where(x => observedMemberIds.Contains(x.Id) &&
                        (tracesBaseline || x.OriginKind != MemberOrigin.Baseline))
            .Select(x => x.Id)
            .ToHashSet();
    }

    private async Task<(int Updated, int Inserted, int Removed)> UpsertMappingsAsync(
        int projectId,
        IReadOnlySet<int> testMemberIds,
        IReadOnlyCollection<SourceTestMappingEntity> mappings,
        CancellationToken cancellationToken)
    {
        var existing = (await _dbContext.SourceTestMappings
                .Include(x => x.TraceSteps)
                .Where(x => x.ProjectId == projectId)
                .ToListAsync(cancellationToken))
            .Where(x => testMemberIds.Contains(x.TestMemberId))
            .ToDictionary(x => (x.SourceMemberId, x.TestMemberId, x.EvidenceKind));

        int updated = 0, inserted = 0;
        foreach (var mapping in mappings)
        {
            var key = (mapping.SourceMemberId, mapping.TestMemberId, mapping.EvidenceKind);
            if (existing.Remove(key, out var row))
            {
                UpdateMapping(row, mapping);
                updated++;
            }
            else
            {
                _dbContext.SourceTestMappings.Add(mapping);
                inserted++;
            }
        }

        // What is left belonged to a traced test but was not reproduced by this trace.
        _dbContext.SourceTestMappings.RemoveRange(existing.Values);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (updated, inserted, existing.Count);
    }

    private static void UpdateMapping(SourceTestMappingEntity row, SourceTestMappingEntity mapping)
    {
        row.IsGrounded = mapping.IsGrounded;
        row.AccessPathStrategy = mapping.AccessPathStrategy;
        row.PathLength = mapping.PathLength;
        row.Confidence = mapping.Confidence;
        row.Summary = mapping.Summary;
        row.ResolverVersion = mapping.ResolverVersion;

        // Steps are matched by index and edited in place: removing and re-adding a step with the
        // same index in one save would collide on the (mapping, step_index) unique index.
        var steps = row.TraceSteps.ToDictionary(x => x.StepIndex);
        foreach (var step in mapping.TraceSteps)
        {
            if (steps.Remove(step.StepIndex, out var current))
            {
                current.FromMemberId = step.FromMemberId;
                current.ToMemberId = step.ToMemberId;
                current.RelationshipKind = step.RelationshipKind;
                current.EdgeSource = step.EdgeSource;
                current.Summary = step.Summary;
            }
            else
            {
                row.TraceSteps.Add(step);
            }
        }

        foreach (var stale in steps.Values) row.TraceSteps.Remove(stale);
    }
}
