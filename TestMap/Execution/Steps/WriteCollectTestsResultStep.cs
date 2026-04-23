using TestMap.App;
using TestMap.Services.CollectInformation;

namespace TestMap.Execution.Steps;

public class WriteCollectTestsResultStep : IPipelineStep
{
    private readonly CollectTestsResultWriter _writer;

    public WriteCollectTestsResultStep(CollectTestsResultWriter writer)
    {
        _writer = writer;
    }

    public Task ExecuteAsync(ProjectContext? context = null)
    {
        return _writer.WriteAsync();
    }
}
