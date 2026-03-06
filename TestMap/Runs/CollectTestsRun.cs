using TestMap.App;
using TestMap.Execution;
using TestMap.Execution.Steps;
using TestMap.Services.CollectInformation;
using TestMap.Services.Database;
using TestMap.Services.Mapping;
using TestMap.Services.RepoOperations;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.Testing;

namespace TestMap.Runs;

public class CollectTestsRun : IPipelineRun
{
    private readonly ProjectContext _context;
    private readonly IRepoOperations _repoOperations;
    private readonly ISqliteDatabaseService _sqliteDatabaseService;
    private readonly IExtractInformationService _extractInformationService;
    private readonly IAnalyzeProjectService _analyzeProjectService;
    private readonly IBuildTestService _buildTestService;
    private readonly IMapUnresolvedService _mapUnresolvedService;

    public CollectTestsRun(
        ProjectContext context,
        IRepoOperations repoOperations,
        ISqliteDatabaseService sqliteDatabaseService,
        IExtractInformationService extractInformationService,
        IAnalyzeProjectService analyzeProjectService,
        IBuildTestService buildTestService,
        IMapUnresolvedService mapUnresolvedService)
    {
        _context = context;
        _repoOperations = repoOperations;
        _sqliteDatabaseService = sqliteDatabaseService;
        _extractInformationService = extractInformationService;
        _analyzeProjectService = analyzeProjectService;
        _buildTestService = buildTestService;
        _mapUnresolvedService = mapUnresolvedService;
    }

    public RunPipeline CreatePipeline()
    {
        // All services come from the context
        var steps = new IPipelineStep[]
        {
            new CloneRepoStep(_repoOperations),
            new LoadDatabaseStep(_sqliteDatabaseService),
            new ExtractInfoStep(_extractInformationService),
            new InsertProjectInfoStep(_sqliteDatabaseService),
            new AnalyzeProjectStep(_analyzeProjectService),
            new BuildTestStep(_buildTestService),
            new MapInfoStep(_mapUnresolvedService)
        };

        return new RunPipeline(steps);
    }
}