/*
 * consulthunter
 * 2025-02-17
 * Options for generating
 * the config file
 * GenerateConfigOptions.cs
 */

using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

public class CheckProjectsOptions : IPipelineOptions
{
    public RunMode Mode => RunMode.CheckProjects;

    public string CheckProjectsConfigFilePath { get; set; } = string.Empty;

    string IPipelineOptions.ConfigFilePath => CheckProjectsConfigFilePath;
}