using TestMap.Models.Configuration;

namespace TestMap.Runs;

public interface IPipelineRunFactory
{
    IPipelineRun Create(RunMode runMode);
}