using CommandLine;
using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

[Verb("static-analysis", HelpText = "Run static project analysis, code metrics, test metadata enrichment, and test smell collection.")]
public class StaticAnalysisOptions : IPipelineOptions
{
    public RunMode Mode => RunMode.StaticAnalysis;

    [Option('c', "config", SetName = "static-analysis", Required = false, HelpText = "Config File path.")]
    public string StaticAnalysisConfigFilePath { get; set; } = string.Empty;

    string IPipelineOptions.ConfigFilePath => StaticAnalysisConfigFilePath;
}
