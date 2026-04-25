using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

/// <summary>
/// CLI options for running AI provider comparison experiments.
/// Example: testmap experiment -c default-config.json -e experiment-config.json
/// </summary>
public class ExperimentOptions : IPipelineOptions
{
    public RunMode Mode => RunMode.Experiment;

    public string ConfigFilePath { get; set; } = string.Empty;

    public string ExperimentConfigFilePath { get; set; } = string.Empty;
}