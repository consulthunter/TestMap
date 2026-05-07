/*
 * consulthunter
 * 2024-11-07
 * CommandLine Options for the
 * collect command
 * CollectOptions.cs
 */

using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

public class CollectTestOptions : IPipelineOptions
{
    public RunMode Mode => RunMode.CollectTests;

    public string CollectConfigFilePath { get; set; } = string.Empty;

    string IPipelineOptions.ConfigFilePath => CollectConfigFilePath;
}