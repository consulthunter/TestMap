using Microsoft.Build.Locator;
using Serilog;
using TestMap.Models;
using TestMap.Services.ProjectOperations;

namespace TestMap.Services;

public class TestMapRunner
{
    // fields
    private int MaxConcurrency { get; set; }
    private List<ProjectModel> _projects;
    public ILogger Logger { get; private set; }
    private ConfigurationService ConfigurationService { get; set; }
    // methods
    public async Task RunAsync()
    {
        var tasks = new List<Task>();

        // Use SemaphoreSlim for concurrency control
        Logger.Information($"Starting runner.");
        Logger.Information($"Number of target projects {_projects.Count}");
        using (var semaphore = new SemaphoreSlim(MaxConcurrency))
        {
            foreach (var project in _projects)
            {
                Logger.Information($"Project number: {_projects.IndexOf(project)}");
                Logger.Information($"Target project {project.ProjectId}");
                await semaphore.WaitAsync();

                Logger.Information($"Creating TestMap {project.ProjectId}.");
                var testMap = new Models.TestMap
                (
                    project, 
                    new CloneRepoService(project), 
                    new SdkManager(project),
                    new BuildSolutionService(project), 
                    new BuildProjectService(project), 
                    new AnalyzeProjectService(project),
                    new DeleteProjectService(project)
                );
                
                tasks.Add(RunTestMapAsync(testMap, semaphore));
            }

            await Task.WhenAll(tasks);
        }
    }

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
    private void InitializeAsync()
    {
        MSBuildLocator.RegisterDefaults();
        ConfigurationService.ConfigureRunAsync().GetAwaiter().GetResult();

        _projects = ConfigurationService.ProjectModels;
        MaxConcurrency = ConfigurationService.MaxConcurrency;

        string logPath = Path.Combine(ConfigurationService.LogsDirPath, ConfigurationService.RunDate,
            $"{ConfigurationService.RunDate}.log");

        // Configure logging for this instance of TestMap using Serilog
        Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(logPath)
            .CreateLogger();
    }
    // constructor
    public TestMapRunner(ConfigurationService configurationService)
    {
        ConfigurationService = configurationService;
        InitializeAsync();
    }
}