/*
 * consulthunter
 * 2024-11-07
 * Creates TestMaps and starts the
 * analysis using semaphores for
 * limited concurrency
 * TestMapRunner.cs
 */

using Microsoft.Build.Locator;
using Serilog;
using TestMap.Models;
using TestMap.Services.ProjectOperations;

namespace TestMap.Services;

public class TestMapRunner
{
    // fields
    private int MaxConcurrency { get; set; }
    private readonly List<ProjectModel> _projects;
    private ILogger Logger { get; set; }

    private IConfigurationService ConfigurationService { get; set; }

    // methods
    /// <summary>
    /// Creates and manages TestMap
    /// concurrency
    /// </summary>
    public async Task RunAsync()
    {
        var tasks = new List<Task>();

        // Use SemaphoreSlim for concurrency control
        Logger.Information($"Starting runner.");
        Logger.Information($"Number of target projects {_projects.Count}");
        // I believe that it still hangs on large projects
        using (var semaphore = new SemaphoreSlim(MaxConcurrency))
        {
            foreach (var project in _projects)
            {
                Logger.Information($"Project number: {_projects.IndexOf(project)}");
                Logger.Information($"Target project {project.ProjectId}");
                await semaphore.WaitAsync();
                
                project.EnsureProjectLogDir();
                project.EnsureProjectOutputDir();

                Logger.Information($"Creating TestMap {project.ProjectId}.");
                var testMap = new Models.TestMap
                (
                    project,
                    new CloneRepoService(project),
                    new SdkManager(project),
                    new BuildSolutionService(project),
                    new AnalyzeProjectService(project),
                    new DeleteProjectService(project)
                );

                tasks.Add(RunTestMapAsync(testMap, semaphore));
            }

            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Starts the TestMap
    /// and releases the TestMap when finished
    /// </summary>
    /// <param name="testMap"></param>
    /// <param name="semaphore"></param>
    private async Task RunTestMapAsync(Models.TestMap testMap, SemaphoreSlim semaphore)
    {
        try
        {
            Logger.Information($"Running TestMap {testMap.ProjectModel.ProjectId}");
            await testMap.RunAsync();
        }
        finally
        {
            Logger.Information($"Finished TestMap {testMap.ProjectModel.ProjectId}");
            semaphore.Release();
            Logger.Information($"Releasing TestMap {testMap.ProjectModel.ProjectId}");
        }
    }
    
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="configurationService">Configuration service for access to variables</param>
    public TestMapRunner(IConfigurationService configurationService)
    {
        ConfigurationService = configurationService;
        
        MSBuildLocator.RegisterDefaults();
        ConfigurationService.ConfigureRunAsync().GetAwaiter().GetResult();

        _projects = ConfigurationService.GetProjectModels();
        MaxConcurrency = ConfigurationService.GetConcurrency();

        var logPath = Path.Combine(ConfigurationService.GetLogsDirectory() ?? string.Empty, ConfigurationService.GetRunDate(),
            $"collection_{ConfigurationService.GetRunDate()}.log");

        // Configure logging for this instance of TestMap using Serilog
        Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(logPath)
            .CreateLogger();
    }
}