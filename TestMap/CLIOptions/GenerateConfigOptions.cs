/*
 * consulthunter
 * 2025-02-17
 * Options for generating
 * the config file
 * GenerateConfigOptions.cs
 */

using CommandLine;

namespace TestMap.CLIOptions;

[Verb("generate-config", HelpText = "Generates the config file.")]
public class GenerateConfigOptions
{
    [Option('p', "path", SetName = "generate-config", Required = true, HelpText = "Config File path.")]
    public string GenerateConfigFilePath { get; set; }
}