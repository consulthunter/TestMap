using CommandLine;
using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

/// <summary>
/// CLI options for running AI provider comparison experiments.
/// Example: testmap experiment -c default-config.json -e experiment-config.json
/// </summary>
[Verb("experiment", HelpText = "Run AI provider comparison experiments for test generation.")]
public class ExperimentOptions : IPipelineOptions
{
    public RunMode Mode => RunMode.Experiment;

    [Option('c', "config", SetName = "experiment", Required = false, 
        HelpText = "Path to the main TestMap configuration JSON file.")]
    public string ConfigFilePath { get; set; } = string.Empty;

    [Option('e', "experiment-config", SetName = "experiment", Required = false,
        HelpText = "Optional path to a separate experiment configuration JSON file. Can be either an ExperimentConfig object or a full object containing an ExperimentConfig section.")]
    public string ExperimentConfigFilePath { get; set; } = string.Empty;
}
