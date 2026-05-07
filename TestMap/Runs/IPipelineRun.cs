using TestMap.Execution;

namespace TestMap.Runs;

public interface IPipelineRun
{
    RunPipeline CreatePipeline();
}