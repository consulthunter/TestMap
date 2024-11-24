/*
 * consulthunter
 * 2024-11-07
 * Initial entry point for the tool
 * Uses CommandLine for CLI Options
 * Program.cs
 */

using System.Reflection;
using CommandLine;
using TestMap.Services;
using Microsoft.Extensions.Configuration;
using TestMap.CLIOptions;

namespace TestMap;

public class Program
{
    /// <summary>
    /// Main
    /// </summary>
    /// <param name="args">Arguments passed from the CLI</param>
    public static async Task Main(string[] args)
    {
        var types = LoadVerbs();			

        await Parser.Default.ParseArguments(args, types)
            .WithParsedAsync(Run);
        
    }
    /// <summary>
    /// Gets the commandline verbs defined for the program.
    /// </summary>
    /// <returns>Array of commandline objects</returns>
    private	static Type[] LoadVerbs()
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();		 
    }
    
    
    private static async Task Run(object obj)
    {
        switch (obj)
        {
            case CollectOptions c:
                await RunCollect(c);
                break;
        }
    }
    /// <summary>
    /// Builds the configuration using options, starts the TestMapRunner
    /// </summary>
    /// <param name="options">CLI options parsed by CommandLine</param>
    private static async Task RunCollect(CollectOptions options)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(options.CollectConfigFilePath, optional: false, reloadOnChange: true)
            .Build();
        var configurationService = new ConfigurationService(config);
        var testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }
}