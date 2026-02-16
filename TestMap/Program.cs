/*
 * consulthunter
 * 2024-11-07
 * Initial entry point for the tool
 * Uses CommandLine for CLI Options
 * Program.cs
 */

using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using TestMap.CLIOptions;
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


    private static async Task Run(object obj)
    {
        switch (obj)
        {
            case CollectTestOptions c:
                await RunCollect(c);
                break;
            case GenerateTestsOptions gt:
                await RunGenTests(gt);
                break;
            case FullAnalysisOptions fa:
                await RunFullAnalysis(fa);
                break;
            case SetupOptions s:
                RunSetup(s);
                break;
            case CheckProjectsOptions cp:
                await RunCheckProjects(cp);
                break;
            case ValidateProjectsOptions vo:
                await RunValidateProjects(vo);
                break;
            case WindowsCheckOptions wc:
                await RunWindowsCheck(wc);
                break;
        }
    }

    /// <summary>
    ///     Builds the configuration using options, starts the TestMapRunner
    /// </summary>
    /// <param name="testOptions">CLI options parsed by CommandLine</param>
    private static async Task RunCollect(CollectTestOptions testOptions)
    {
        Utilities.Utilities.Load();
        var config = new ConfigurationBuilder()
            .AddJsonFile(ConfigurationLocation(testOptions.CollectConfigFilePath), false, true)
            .Build();
        var configObj = new TestMapConfig();
        config.Bind(configObj);
        var configurationService = new ConfigurationService(configObj);
        configurationService.RunMode = testOptions.Mode;
        configurationService.SetSecrets();
        var testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }

    /// <summary>
    ///     Builds the configuration using options, starts the TestMapRunner
    /// </summary>
    /// <param name="options">CLI options parsed by CommandLine</param>
    private static async Task RunGenTests(GenerateTestsOptions options)
    {
        Utilities.Utilities.Load();
        var config = new ConfigurationBuilder()
            .AddJsonFile(ConfigurationLocation(options.GenTestsConfigFilePath), false, true)
            .Build();
        var configObj = new TestMapConfig();
        config.Bind(configObj);
        var configurationService = new ConfigurationService(configObj);
        configurationService.RunMode = options.Mode;
        configurationService.SetSecrets();
        var testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }

    /// <summary>
    ///     Builds the configuration using options, starts the TestMapRunner
    /// </summary>
    /// <param name="options">CLI options parsed by CommandLine</param>
    private static async Task RunFullAnalysis(FullAnalysisOptions options)
    {
        Utilities.Utilities.Load();
        var config = new ConfigurationBuilder()
            .AddJsonFile(ConfigurationLocation(options.FullAnalysisConfigFilePath), false, true)
            .Build();
        var configObj = new TestMapConfig();
        config.Bind(configObj);
        var configurationService = new ConfigurationService(configObj);
        configurationService.RunMode = options.Mode;
        configurationService.SetSecrets();
        var testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }

    /// <summary>
    ///     Generates the correct configuration for TestMap
    /// </summary>
    /// <param name="options">CLI options parsed by CommandLine</param>
    private static void RunSetup(SetupOptions options)
    {
        var setupService = new SetupService(options.BasePath);
        setupService.Setup(options.OverwriteFile);
    }
    /// <summary>
    ///     Generates the correct configuration for TestMap
    /// </summary>
    /// <param name="options">CLI options parsed by CommandLine</param>
    private static async Task RunCheckProjects(CheckProjectsOptions options)
    {
        Utilities.Utilities.Load();
        var config = new ConfigurationBuilder()
            .AddJsonFile(ConfigurationLocation(options.CheckProjectsConfigFilePath), false, true)
            .Build();
        var configObj = new TestMapConfig();
        config.Bind(configObj);
        var configurationService = new ConfigurationService(configObj);
        configurationService.RunMode = options.Mode;
        configurationService.SetSecrets();
        var testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }
    

    /// <summary>
    ///     Generates the correct configuration for TestMap
    /// </summary>
    /// <param name="options">CLI options parsed by CommandLine</param>
    private static async Task RunValidateProjects(ValidateProjectsOptions options)
    {
        Utilities.Utilities.Load();
        var config = new ConfigurationBuilder()
            .AddJsonFile(ConfigurationLocation(options.ValidateProjectsConfigFilePath), false, true)
            .Build();
        var configObj = new TestMapConfig();
        config.Bind(configObj);
        var configurationService = new ConfigurationService(configObj);
        configurationService.RunMode = options.Mode;
        configurationService.SetSecrets();
        var testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }
    
    private static async Task RunWindowsCheck(WindowsCheckOptions options)
    {
        Utilities.Utilities.Load();
        var config = new ConfigurationBuilder()
            .AddJsonFile(ConfigurationLocation(options.WindowsCheckConfigFilePath), false, true)
            .Build();
        var configObj = new TestMapConfig();
        config.Bind(configObj);
        var configurationService = new ConfigurationService(configObj);
        configurationService.RunMode = options.Mode;
        configurationService.SetSecrets();
        var testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }

    private static string ConfigurationLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Path.Join(Directory.GetCurrentDirectory(), "Config", "default-config.json");
        return path;
    }
}