using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

public class GenerateTestsOptions : IPipelineOptions
{
    public RunMode Mode => RunMode.GenerateTests;

    public string GenTestsConfigFilePath { get; set; } = string.Empty;

    string IPipelineOptions.ConfigFilePath => GenTestsConfigFilePath;
}