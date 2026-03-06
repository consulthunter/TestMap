using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TestMap.Execution;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Runs;
using TestMap.Services.CollectInformation;
using TestMap.Services.Configuration;
using TestMap.Services.Database;
using TestMap.Services.Evaluation;
using TestMap.Services.Mapping;
using TestMap.Services.ProjectOperations;
using TestMap.Services.RepoOperations;
using TestMap.Services.RepoOperations.Clone;
using TestMap.Services.RepoOperations.Delete;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.Testing;

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
        _maxConcurrency = _config.Settings.MaxConcurrency;
        _runMode = configurationService.RunMode;

        // setup logger
        var logPath = Path.Combine(_config.FilePaths.LogsDirPath ?? string.Empty,
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

        var runFactory = provider.GetRequiredService<IPipelineRunFactory>();
        var run = runFactory.Create(_runMode);

        var pipeline = run.CreatePipeline();
        var projectPipeline = new ProjectPipelineExecutor(context, pipeline);

        await projectPipeline.RunAsync();
    }

    private ServiceProvider BuildProjectServiceProvider(ProjectContext context)
    {
        var services = new ServiceCollection();

        services.AddSingleton(ConfigurationService);
        services.AddSingleton(_config);
        services.AddScoped<ProjectContext>(_ => context);

        services.AddScoped<SqliteDatabaseService>();
        services.AddScoped<ISqliteDatabaseService>(sp => sp.GetRequiredService<SqliteDatabaseService>());

        services.AddScoped<BuildTestService>();
        services.AddScoped<IBuildTestService>(sp => sp.GetRequiredService<BuildTestService>());

        services.AddScoped<IRepoOperations, RepoOperations>();
        services.AddScoped<IAnalyzeProjectService, AnalyzeProjectService>();
        services.AddScoped<IExtractInformationService, ExtractInformationService>();
        services.AddScoped<IMapUnresolvedService, MapUnresolvedService>();
        services.AddScoped<IGenerateTestService, GenerateTestService>();
        services.AddScoped<ICheckProjectsService, CheckProjectsService>();
        services.AddScoped<IValidateProjectsService, ValidateProjectsService>();
        services.AddScoped<IWindowsCheckService, WindowsCheckService>();
        services.AddScoped<IFullAnalysisService, FullAnalysisService>();
        services.AddScoped<ICloneRepoService, CloneRepoService>();
        services.AddScoped<IDeleteProjectService, DeleteProjectService>();

        services.AddTransient<IPipelineRunFactory, PipelineRunFactory>();

        services.AddTransient<CollectTestsRun>();

        return services.BuildServiceProvider();
    }
}