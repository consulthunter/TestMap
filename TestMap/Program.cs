/*
 * consulthunter
 * 2024-11-07
 * Initial entry point for the tool
 * Uses CommandLine for CLI Options
 * Program.cs
 */

using System.CommandLine;
using System.Text.Json;
using TestMap.App;
using TestMap.CLIOptions;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Services;
using TestMap.Services.Configuration;
using TestMap.Services.ProjectDiscovery;

namespace TestMap;

public class Program
{
    public static Func<SetupOptions, SetupService> SetupServiceFactory { get; set; } =
        options => new SetupService(options.BasePath);

    /// <summary>
    ///     Main
    /// </summary>
    /// <param name="args">Arguments passed from the CLI</param>
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = BuildRootCommand();
        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("TestMap");

        rootCommand.Subcommands.Add(CreateSetupCommand());
        rootCommand.Subcommands.Add(CreatePipelineCommand(
            "check-projects",
            "Checks projects in the target file to likely contain tests.",
            configPath => new CheckProjectsOptions
            {
                CheckProjectsConfigFilePath = configPath
            }));
        rootCommand.Subcommands.Add(CreatePipelineCommand(
            "collect-tests",
            "Collect tests from source code.",
            configPath => new CollectTestOptions
            {
                CollectConfigFilePath = configPath
            }));
        rootCommand.Subcommands.Add(CreatePipelineCommand(
            "static-analysis",
            "Run static project analysis, code metrics, test metadata enrichment, and test smell collection.",
            configPath => new StaticAnalysisOptions
            {
                StaticAnalysisConfigFilePath = configPath
            }));
        rootCommand.Subcommands.Add(CreatePipelineCommand(
            "generate-tests",
            "Generates tests for the repository.",
            configPath => new GenerateTestsOptions
            {
                GenTestsConfigFilePath = configPath
            }));
        rootCommand.Subcommands.Add(CreateExperimentCommand());

        return rootCommand;
    }

    private static Command CreatePipelineCommand(
        string name,
        string description,
        Func<string, IPipelineOptions> createOptions)
    {
        var configOption = CreateConfigOption("Config File path.");
        var command = new Command(name, description);
        command.Options.Add(configOption);
        command.SetAction(async parseResult =>
        {
            var configPath = parseResult.GetValue(configOption) ?? string.Empty;
            await Run(createOptions(configPath));
        });

        return command;
    }

    private static Command CreateExperimentCommand()
    {
        var configOption = CreateConfigOption("Path to the main TestMap configuration JSON file.");
        var command = new Command("experiment", "Run AI provider comparison experiments for test generation.");
        command.Options.Add(configOption);
        command.SetAction(async parseResult =>
        {
            await Run(new ExperimentOptions
            {
                ConfigFilePath = parseResult.GetValue(configOption) ?? string.Empty
            });
        });

        return command;
    }

    private static Command CreateSetupCommand()
    {
        var basePathOption = new Option<string>("--base-path", "-b")
        {
            Description = "Base Path for the project."
        };
        var overwriteOption = new Option<bool>("--overwrite", "-o")
        {
            Description = "Overwrite Config File."
        };
        var command = new Command("setup", "Generates the config file.");
        command.Options.Add(basePathOption);
        command.Options.Add(overwriteOption);
        command.SetAction(async parseResult =>
        {
            await Run(new SetupOptions
            {
                BasePath = parseResult.GetValue(basePathOption) ?? string.Empty,
                OverwriteFile = parseResult.GetValue(overwriteOption)
            });
        });

        return command;
    }

    private static Option<string> CreateConfigOption(string description)
    {
        return new Option<string>("--config", "-c")
        {
            Description = description
        };
    }

    /// <summary>
    /// Routes command-line arguments to the appropriate execution handler.
    /// </summary>
    /// <param name="obj">Parsed command-line options object.</param>
    private static async Task Run(object obj)
    {
        switch (obj)
        {
            case ExperimentOptions experimentOptions:
                await RunExperimentPipeline(experimentOptions);
                break;
            case IPipelineOptions pipelineOptions:
                await RunPipeline(pipelineOptions);
                break;
            case SetupOptions setupOptions:
                RunSetup(setupOptions);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown options type: {obj.GetType().Name}");
        }
    }

    /// <summary>
    /// Unified pipeline execution method for all pipeline-based commands.
    /// Loads configuration, initializes services, and runs the pipeline.
    /// </summary>
    /// <param name="options">Pipeline options implementing IPipelineOptions.</param>
    private static async Task RunPipeline(IPipelineOptions options)
    {
        // Load utilities and secrets
        Utilities.Utilities.Load();

        // Load and bind configuration
        var configObj = LoadMainConfiguration(options.ConfigFilePath);

        // Configure service with run mode and secrets
        var configurationService = new ConfigurationService(configObj)
        {
            RunMode = options.Mode
        };
        configurationService.SetSecrets();

        // Create and run the pipeline coordinator
        var pipelineRunner = new ProjectRunCoordinator(configurationService);
        await pipelineRunner.RunAsync();
    }

    private static async Task RunExperimentPipeline(ExperimentOptions options)
    {
        Utilities.Utilities.Load();

        var configObj = LoadMainConfiguration(options.ConfigFilePath);

        var configurationService = new ConfigurationService(configObj)
        {
            RunMode = options.Mode
        };
        configurationService.SetSecrets();

        var pipelineRunner = new ProjectRunCoordinator(configurationService);
        await pipelineRunner.RunAsync();
    }

    /// <summary>
    /// Generates the correct configuration for TestMap.
    /// </summary>
    /// <param name="options">Setup options parsed by CommandLine.</param>
    private static void RunSetup(SetupOptions options)
    {
        var setupService = SetupServiceFactory(options);
        setupService.Setup(options.OverwriteFile);
    }

    private static string ConfigurationLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Path.Join(Directory.GetCurrentDirectory(), "Config", "default-config.json");
        return path;
    }

    private static TestMapConfig LoadMainConfiguration(string path)
    {
        var json = File.ReadAllText(ConfigurationLocation(path));
        var configObj = JsonSerializer.Deserialize<TestMapConfig>(json, ConfigJsonSerializer.CreateOptions());

        return configObj
               ?? throw new InvalidOperationException(
                   $"Config file '{ConfigurationLocation(path)}' could not be parsed.");
    }
}
