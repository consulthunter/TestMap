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

[Verb("collect-tests", HelpText = "Collect tests from source code.")]
public class CollectTestOptions
{
    public RunMode Mode => RunMode.CollectTests;

    [Option('c', "config", SetName = "collect", Required = false, HelpText = "Config File path.")]
    public string CollectConfigFilePath { get; set; }
}