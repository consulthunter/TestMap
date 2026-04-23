/*
 * consulthunter
 * 2025-02-17
 * Options for generating
 * the config file
 * GenerateConfigOptions.cs
 */

using CommandLine;
using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

[Verb("setup", HelpText = "Generates the config file.")]
public class SetupOptions
{
    public RunMode Mode => RunMode.Setup;

    [Option('b', "base-path", SetName = "setup", Required = false, HelpText = "Base Path for the project.")]
    public string BasePath { get; set; } = string.Empty;

    [Option('o', "overwrite", SetName = "setup", Required = false, HelpText = "Overwrite Config File.")]
    public bool OverwriteFile { get; set; }
}
