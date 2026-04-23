using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

/// <summary>
/// Interface for CLI options that trigger pipeline execution.
/// </summary>
public interface IPipelineOptions
{
    /// <summary>
    /// Gets the run mode for the pipeline.
    /// </summary>
    RunMode Mode { get; }

    /// <summary>
    /// Gets the configuration file path.
    /// </summary>
    string ConfigFilePath { get; }
}
