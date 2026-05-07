/*
 * consulthunter
 * 2025-02-17
 * Options for generating
 * the config file
 * GenerateConfigOptions.cs
 */

using TestMap.Models.Configuration;

namespace TestMap.CLIOptions;

public class SetupOptions
{
    public RunMode Mode => RunMode.Setup;

    public string BasePath { get; set; } = string.Empty;

    public bool OverwriteFile { get; set; }
}