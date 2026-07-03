using TestMap.App;

namespace TestMap.Execution;

public class ProjectPipelineExecutor
{
    private readonly ProjectContext _context;
    private readonly RunPipeline _pipeline;

    // Inject the context and the pre-configured pipeline via DI
    public ProjectPipelineExecutor(ProjectContext context, RunPipeline pipeline)
    {
        _context = context;
        _pipeline = pipeline;
    }

    public async Task RunAsync()
    {
        // All passes get the DI-injected context
        await _pipeline.RunAsync(_context);
    }
}