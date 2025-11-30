/*
 * consulthunter
 * 2024-11-07
 * CommandLine Options for the
 * collect command
 * CollectOptions.cs
 */

using CommandLine;
using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

[Verb("full-analysis", HelpText = "Collect tests from source code.")]
public class FullAnalysisOptions
{
    public RunMode Mode => RunMode.FullAnalysis;

    [Option('c', "config", SetName = "collect", Required = false, HelpText = "Config File path.")]
    public string FullAnalysisConfigFilePath { get; set; }
}