using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

public class StaticAnalysisOptions : IPipelineOptions
{
    public RunMode Mode => RunMode.StaticAnalysis;

    public string StaticAnalysisConfigFilePath { get; set; } = string.Empty;

    string IPipelineOptions.ConfigFilePath => StaticAnalysisConfigFilePath;
}