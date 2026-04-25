using Microsoft.Extensions.DependencyInjection;
using TestMap.Models.Configuration;

namespace TestMap.Runs;

/// <summary>
/// Factory for creating pipeline runs based on the specified run mode.
/// Uses a dictionary-based lookup for better maintainability and extensibility.
/// </summary>
public class PipelineRunFactory : IPipelineRunFactory
{
    private readonly IServiceProvider _provider;

    /// <summary>
    /// Maps run modes to their corresponding pipeline run implementation types.
    /// Add new mappings here when creating new pipeline runs.
    /// </summary>
    private static readonly Dictionary<RunMode, Type> RunTypeMap = new()
    {
        [RunMode.CheckProjects] = typeof(CheckProjectsRun),
        [RunMode.CollectTests] = typeof(CollectTestsRun),
        [RunMode.GenerateTests] = typeof(GenerateTestsRun),
        [RunMode.Experiment] = typeof(ExperimentRun),
        [RunMode.StaticAnalysis] = typeof(StaticAnalysisRun)
    };

    public PipelineRunFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Creates a pipeline run instance for the specified run mode.
    /// </summary>
    /// <param name="runMode">The mode of the pipeline run to create.</param>
    /// <returns>An instance of the pipeline run.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no pipeline run is registered for the specified mode.</exception>
    public IPipelineRun Create(RunMode runMode)
    {
        if (!RunTypeMap.TryGetValue(runMode, out var runType))
            throw new InvalidOperationException(
                $"No pipeline run registered for mode: {runMode}. " +
                $"Available modes: {string.Join(", ", RunTypeMap.Keys)}");

        return (IPipelineRun)_provider.GetRequiredService(runType);
    }
}