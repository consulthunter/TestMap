using TestMap.App;

namespace TestMap.Execution;

public interface IPipelineStep
{
    Task ExecuteAsync(ProjectContext context);
}