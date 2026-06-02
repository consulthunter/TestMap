using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Services.StaticAnalysis;

public class SourceTestMappingRefreshService
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

    public async Task RefreshForProjectAsync(int projectId, int solutionId, CancellationToken cancellationToken = default)
    {
        var mappings = await _roslynTraceService.TraceAsync(projectId, solutionId, cancellationToken) ?? [];
        await ReplaceMappingsAsync(projectId, mappings, cancellationToken);
        _context.Logger?.Information(
            "Refreshed {Count} source-test mapping(s) for project {ProjectId} using roslyn-source-test-trace-v1.",
            mappings.Count,
            projectId);
    }

    private async Task ReplaceMappingsAsync(
        int projectId,
        IReadOnlyCollection<SourceTestMappingEntity> mappings,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.SourceTestMappings
            .Where(x => x.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0) _dbContext.SourceTestMappings.RemoveRange(existing);
        if (mappings.Count > 0) _dbContext.SourceTestMappings.AddRange(mappings);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
