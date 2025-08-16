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
            case FullAnalysisOptions f:
                await RunFullAnalysis(f);
                break;
            case GenerateConfigOptions g:
                RunSetup(g);
                break;
        }
    }

    /// <summary>
    ///     Builds the configuration using options, starts the TestMapRunner
    /// </summary>
    /// <param name="testOptions">CLI options parsed by CommandLine</param>
    private static async Task RunCollect(CollectTestOptions testOptions)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(testOptions.CollectConfigFilePath, false, true)
            .Build();
        var configObj = new TestMapConfig();
        config.Bind(configObj);
        var configurationService = new ConfigurationService(configObj);
        configurationService.SetRunMode("collect-tests");
        var testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }
    /// <summary>
    ///     Builds the configuration using options, starts the TestMapRunner
    /// </summary>
    /// <param name="options">CLI options parsed by CommandLine</param>
    private static async Task RunGenTests(GenerateTestsOptions options)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(options.GenTestsConfigFilePath, false, true)
            .Build();
        var configObj = new TestMapConfig();
        config.Bind(configObj);
        var configurationService = new ConfigurationService(configObj);
        configurationService.SetRunMode("generate-tests");
        var testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }
    
    /// <summary>
    ///     Builds the configuration using options, starts the TestMapRunner
    /// </summary>
    /// <param name="options">CLI options parsed by CommandLine</param>
    private static async Task RunFullAnalysis(FullAnalysisOptions options)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(options.FullAnalysisConfigFilePath, false, true)
            .Build();
        var configObj = new TestMapConfig();
        config.Bind(configObj);
        var configurationService = new ConfigurationService(configObj);
        configurationService.SetRunMode("full-analysis");
        var testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }
    
    /// <summary>
    ///     Generates the correct configuration for TestMap
    /// </summary>
    /// <param name="options">CLI options parsed by CommandLine</param>
    private static void RunSetup(GenerateConfigOptions options)
    {
        var configPath = options.GenerateConfigFilePath;
        var basePath = options.BasePath;
        var configurationService = new GenerateConfigurationService(configPath, basePath);
        configurationService.GenerateConfiguration();
    }
}