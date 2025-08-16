using CommandLine;

namespace TestMap.CLIOptions;

[Verb("export-data", HelpText = "Exports data from the database.")]
public class ExportDataOptions
{
    [Option('c', "config", SetName = "export-data", Required = true, HelpText = "Config File path.")]
    public string ExportDataConfigFilePath { get; set; }
}