using CommandLine;
using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

[Verb("generate-tests", HelpText = "Generates tests for the repository.")]
public class GenerateTestsOptions : IPipelineOptions
{
    public RunMode Mode => RunMode.GenerateTests;

    [Option('c', "config", SetName = "generate-tests", Required = false, HelpText = "Config File path.")]
    public string GenTestsConfigFilePath { get; set; } = string.Empty;

    string IPipelineOptions.ConfigFilePath => GenTestsConfigFilePath;
}
