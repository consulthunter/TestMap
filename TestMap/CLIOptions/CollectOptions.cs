/*
 * consulthunter
 * 2024-11-07
 * CommandLine Options for the
 * collect command
 * CollectOptions.cs
 */

using CommandLine;

namespace TestMap.CLIOptions;

[Verb("collect", HelpText = "Collect tests from source code.")]
public class CollectOptions
{
    [Option('c', "config", SetName = "collect", Required = true, HelpText = "Config File path.")]
    public string CollectConfigFilePath { get; set; }
}