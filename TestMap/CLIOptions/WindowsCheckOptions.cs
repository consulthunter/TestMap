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

[Verb("windows-check", HelpText = "For previous test runs, check logs to determine if Windows is needed to collect code coverage.")]
public class WindowsCheckOptions
{
    public RunMode Mode => RunMode.WindowsCheck;

    [Option('c', "config", SetName = "windows-check", Required = false, HelpText = "Config File path.")]
    public string WindowsCheckConfigFilePath { get; set; }
}