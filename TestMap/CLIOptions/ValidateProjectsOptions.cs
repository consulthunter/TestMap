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

[Verb("validate-projects", HelpText = "Validates projects in the target file to likely contain tests.")]
public class ValidateProjectsOptions
{
    public RunMode Mode => RunMode.ValidateProjects;

    [Option('c', "config", SetName = "validate-projects", Required = false, HelpText = "Config File path.")]
    public string ValidateProjectsConfigFilePath { get; set; }
}