using TestMap.Execution;
using TestMap.Execution.Steps;

namespace TestMap.Runs;

public class CheckProjectsRun : IPipelineRun
{
    private readonly CheckProjectsStep _checkProjectsStep;

    public CheckProjectsRun(CheckProjectsStep checkProjectsStep)
    {
        _checkProjectsStep = checkProjectsStep;
    }

    public RunPipeline CreatePipeline()
    {
        return new RunPipeline([
            _checkProjectsStep
        ]);
    }
}