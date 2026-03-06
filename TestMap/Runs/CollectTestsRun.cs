using TestMap.App;
using TestMap.Execution;
using TestMap.Execution.Steps;
using TestMap.Services.RepoOperations;

namespace TestMap.Runs;

public class CollectTestsRun : IPipelineRun
{
    private readonly ProjectContext _context;
    private readonly IRepoOperations _repoOperations;

    public CollectTestsRun(
        ProjectContext context,
        IRepoOperations repoOperations)
    {
        _context = context;
        _repoOperations = repoOperations;
        
    }

    public RunPipeline CreatePipeline()
    {
        // All services come from the context
        var passes = new IPipelineStep[]
        {
            new CloneRepoStep(_repoOperations)
        };

        return new RunPipeline(passes);
    }
}