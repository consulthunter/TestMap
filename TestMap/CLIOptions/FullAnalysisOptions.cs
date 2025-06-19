using CommandLine;

namespace TestMap.CLIOptions;

[Verb("full-analysis", HelpText = "Collects tests from source code, generates replacement tests, and outputs the results.")]
public class FullAnalysisOptions
{
    [Option('c', "config", SetName = "full-analysis", Required = true, HelpText = "Config File path.")]
    public string FullAnalysisConfigFilePath { get; set; }

}