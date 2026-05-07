using TestMap.App;

namespace TestMap.Services.StaticAnalysis.Enrichment;

public class XNoseNextService
{
    private readonly ProjectContext _context;
    private readonly ITestSmellService _testSmellService;

    public XNoseNextService(ProjectContext context, ITestSmellService testSmellService)
    {
        _context = context;
        _testSmellService = testSmellService;
    }

    public async Task Analyze(string solutionOrProjectPath, CancellationToken cancellationToken = default)
    {
        var projectId = _context.Project.DbId;
        if (projectId <= 0)
        {
            _context.Logger?.Warning(
                "Skipping test smell collection because project {ProjectName} has not been persisted.",
                _context.Project.RepoName);
            return;
        }

        await _testSmellService.CollectAsync(solutionOrProjectPath, projectId, cancellationToken);
    }
}