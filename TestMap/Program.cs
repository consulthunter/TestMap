/*
 * consulthunter
 * 2024-11-07
 * Initial entry point for the tool
 * Uses CommandLine for CLI Options
 * Program.cs
 */

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandLine;
using Microsoft.Extensions.Configuration;
using TestMap.App;
using TestMap.CLIOptions;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Services;
using TestMap.Services.Configuration;
using TestMap.Services.ProjectOperations;
namespace TestMap;

public class Program
{
    /// <summary>
    ///     Main
    /// </summary>
    /// <param name="args">Arguments passed from the CLI</param>
    public static async Task Main(string[] args)
    {
        var types = LoadVerbs();

        await Parser.Default.ParseArguments(args, types)
            .WithParsedAsync(Run);
    }

    /// <summary>
    ///     Gets the commandline verbs defined for the program.
    /// </summary>
    /// <returns>Array of commandline objects</returns>
    private static Type[] LoadVerbs()
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
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
        var config = new ConfigurationBuilder()
            .AddJsonFile(ConfigurationLocation(options.ConfigFilePath), optional: false, reloadOnChange: true)
            .Build();

        var configObj = new TestMapConfig();
        config.Bind(configObj);

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

        var config = new ConfigurationBuilder()
            .AddJsonFile(ConfigurationLocation(options.ConfigFilePath), optional: false, reloadOnChange: true)
            .Build();

        var configObj = new TestMapConfig();
        config.Bind(configObj);

        if (!string.IsNullOrWhiteSpace(options.ExperimentConfigFilePath))
        {
            configObj.ExperimentConfig = LoadExperimentConfiguration(options.ExperimentConfigFilePath);
        }

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
        var setupService = new SetupService(options.BasePath);
        setupService.Setup(options.OverwriteFile);
    }

    private static string ConfigurationLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Path.Join(Directory.GetCurrentDirectory(), "Config", "default-config.json");
        return path;
    }

    private static Models.Experiment.ExperimentConfiguration LoadExperimentConfiguration(string path)
    {
        var json = File.ReadAllText(ConfigurationLocation(path));
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        serializerOptions.Converters.Add(new JsonStringEnumConverter());

        var wrapper = JsonSerializer.Deserialize<ExperimentConfigWrapper>(json, serializerOptions);

        if (wrapper?.ExperimentConfig != null)
        {
            return wrapper.ExperimentConfig;
        }

        var directConfig = JsonSerializer.Deserialize<Models.Experiment.ExperimentConfiguration>(json, serializerOptions);

        if (directConfig == null)
        {
            throw new InvalidOperationException(
                $"Experiment config file '{path}' could not be parsed as either an ExperimentConfig section or an experiment config object.");
        }

        return directConfig;
    }

    private sealed class ExperimentConfigWrapper
    {
        public Models.Experiment.ExperimentConfiguration? ExperimentConfig { get; set; }
    }
}
