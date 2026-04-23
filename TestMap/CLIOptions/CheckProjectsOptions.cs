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

[Verb("check-projects", HelpText = "Checks projects in the target file to likely contain tests.")]
public class CheckProjectsOptions : IPipelineOptions
{
    public RunMode Mode => RunMode.CheckProjects;

    [Option('c', "config", SetName = "check-projects", Required = false, HelpText = "Config File path.")]
    public string CheckProjectsConfigFilePath { get; set; } = string.Empty;

    string IPipelineOptions.ConfigFilePath => CheckProjectsConfigFilePath;
}
