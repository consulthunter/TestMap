using Microsoft.Extensions.DependencyInjection;
using TestMap.Models.Configuration;

namespace TestMap.Runs;

public class PipelineRunFactory : IPipelineRunFactory
{
    private readonly IServiceProvider _provider;

    public PipelineRunFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public IPipelineRun Create(RunMode runMode)
    {
        return runMode switch
        {
            RunMode.CollectTests => _provider.GetRequiredService<CollectTestsRun>(),
            _ => throw new ArgumentOutOfRangeException(nameof(runMode), runMode, null)
        };
    }
}