using CommandLine;
namespace TestMap.CLIOptions;

[Verb("generate-tests", HelpText = "Generates tests for the repository.")]
public class GenerateTestsOptions
{

    [Option('c', "config", SetName = "generate-tests", Required = true, HelpText = "Config File path.")]
    public string GenTestsConfigFilePath { get; set; }
    
}