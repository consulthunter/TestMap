using Microsoft.Build.Locator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TestMap.Execution;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Persistence.Ef;
using TestMap.Runs;
using TestMap.Services;
using TestMap.Services.Configuration;

namespace TestMap.App;

public class ProjectRunCoordinator
{
    private readonly List<ProjectModel> _projects;
    private readonly TestMapConfig _config;
    private readonly ILogger _logger;
    private readonly int _maxConcurrency;
    private readonly RunMode _runMode;
    private IConfigurationService ConfigurationService { get; }

    public ProjectRunCoordinator(IConfigurationService configurationService)
    {
        ConfigurationService = configurationService;

        MSBuildLocator.RegisterDefaults();
        configurationService.ConfigureRunAsync().GetAwaiter().GetResult();

        _config = configurationService.Config;
        _projects = configurationService.ProjectModels;
        _maxConcurrency = _config.RuntimeConfig.MaxConcurrency;
        _runMode = configurationService.RunMode;

        var logPath = Path.Combine(
            _config.RuntimeConfig.FilePaths.LogsDirPath ?? string.Empty,
            configurationService.RunDate,
            $"{_runMode}_{configurationService.RunDate}.log");

        _logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(logPath)
            .CreateLogger();
    }

    public async Task RunAsync()
    {
        _logger.Information("Starting pipeline runner for {ProjectCount} projects", _projects.Count);

        using var semaphore = new SemaphoreSlim(_maxConcurrency);

        var tasks = _projects.Select(async project =>
        {
            await semaphore.WaitAsync();
            try
            {
                await RunProjectAsync(project);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task RunProjectAsync(ProjectModel project)
    {
        project.EnsureProjectLogDir();
        project.EnsureProjectOutputDir();

        var context = new ProjectContext(project);
        using var provider = BuildProjectServiceProvider(context);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestMapDbContext>();
            var databaseInitializer = scope.ServiceProvider.GetRequiredService<TestMapDatabaseInitializer>();
            await databaseInitializer.InitializeAsync(db);
        }

        var runFactory = provider.GetRequiredService<IPipelineRunFactory>();
        var run = runFactory.Create(_runMode);

        var pipeline = run.CreatePipeline();
        var projectPipeline = new ProjectPipelineExecutor(context, pipeline);

        await projectPipeline.RunAsync();
    }

    private ServiceProvider BuildProjectServiceProvider(ProjectContext context)
    {
        var services = new ServiceCollection();
        services.AddTestMapServices(ConfigurationService, _config, context);
        return services.BuildServiceProvider();
    }
}
