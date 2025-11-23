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
}