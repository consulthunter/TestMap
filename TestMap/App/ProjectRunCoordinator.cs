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
using TestMap.Models.Targets;
using TestMap.Services.RepoOperations;
using TestMap.Services.Targets;
using TestMap.Services.TestExecution.Collection;
using System.Globalization;

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
            configurationService.RunStartedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            $"{_runMode}_{configurationService.RunStartedAtUtc:HH-mm-ss}.log");

        _logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(logPath)
            .CreateLogger();
    }

    public async Task RunAsync()
    {
        _logger.Information("Starting pipeline runner for {ProjectCount} projects", _projects.Count);
        if (_projects.Count == 0)
        {
            _logger.Error("No projects were loaded from the configured target source");
            (_logger as IDisposable)?.Dispose();
            throw new InvalidOperationException("No projects were loaded from the configured target source.");
        }

        if (_runMode == RunMode.CollectTests)
            await CollectTestsResultWriter.InitializeReportAsync(
                _config,
                new ProjectContext(_projects[0]));

        TargetExecutionReportWriter? executionReport = null;
        if (ConfigurationService.TargetManifest is not null &&
            ConfigurationService.TargetManifestSha256 is not null &&
            ConfigurationService.TargetExecutionReportPath is not null)
        {
            executionReport = new TargetExecutionReportWriter(new AtomicFilePublisher());
            await executionReport.InitializeAsync(
                ConfigurationService.TargetExecutionReportPath,
                ConfigurationService.TargetManifest,
                ConfigurationService.TargetManifestSha256);
        }

        using var semaphore = new SemaphoreSlim(_maxConcurrency);

        var tasks = _projects.Select(async project =>
        {
            await semaphore.WaitAsync();
            try
            {
                try
                {
                    return await RunProjectAsync(project, executionReport);
                }
                catch (Exception exception) when (project.RepositoryTarget is not null && executionReport is not null)
                {
                    await executionReport.UpdateAsync(
                        project.RepositoryTarget.TargetId,
                        TargetExecutionStatus.PipelineFailed,
                        project.MaterializedRevision?.ResolvedCommit,
                        "Target failed before or outside pipeline execution.",
                        "coordinator",
                        exception.GetType().Name,
                        CancellationToken.None);
                    throw;
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        try
        {
            var results = await Task.WhenAll(tasks);
            var failedProjects = results.Count(succeeded => !succeeded);
            if (failedProjects > 0)
                throw new InvalidOperationException(
                    $"{failedProjects} of {_projects.Count} project pipelines failed. See the target execution report and project logs for details.");
            _logger.Information("Pipeline runner finished processing {ProjectCount} projects", _projects.Count);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Pipeline runner failed while processing {ProjectCount} projects", _projects.Count);
            throw;
        }
        finally
        {
            (_logger as IDisposable)?.Dispose();
        }
    }

    private async Task<bool> RunProjectAsync(ProjectModel project, TargetExecutionReportWriter? executionReport)
    {
        project.EnsureProjectLogDir();

        var context = new ProjectContext(project);
        using var provider = BuildProjectServiceProvider(context);
        var workspacePrepared = false;

        try
        {
            if (project.RepositoryTarget is not null)
            {
                await provider.GetRequiredService<IRepoOperations>().PrepareRepositoryAsync();
                workspacePrepared = true;
                await executionReport!.UpdateAsync(
                    project.RepositoryTarget.TargetId,
                    TargetExecutionStatus.Materialized,
                    context.VerifiedBaseCommit,
                    "Target revision was materialized and verified.");
            }

            project.EnsureProjectOutputDir();
            Directory.CreateDirectory(Path.GetDirectoryName(project.DatabasePath!)!);

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

            if (project.RepositoryTarget is null) workspacePrepared = true;
            await projectPipeline.RunAsync();
            if (project.RepositoryTarget is not null)
                await executionReport!.UpdateAsync(
                    project.RepositoryTarget.TargetId,
                    TargetExecutionStatus.Completed,
                    context.VerifiedBaseCommit,
                    "Target pipeline completed.");
            return true;
        }
        catch (RepositoryMaterializationException exception) when (project.RepositoryTarget is not null)
        {
            var status = exception.Status switch
            {
                MaterializationStatus.WorkspaceBusy => TargetExecutionStatus.WorkspaceBusy,
                MaterializationStatus.AuthenticationFailed => TargetExecutionStatus.AuthenticationFailed,
                MaterializationStatus.RepositoryUnavailable => TargetExecutionStatus.RepositoryUnavailable,
                MaterializationStatus.CommitUnavailable => TargetExecutionStatus.CommitUnavailable,
                _ => TargetExecutionStatus.MaterializationFailed
            };
            await executionReport!.UpdateAsync(
                project.RepositoryTarget.TargetId, status, null, "Target materialization failed.",
                "materialization", exception.Status.ToString());
            project.Logger?.Error("Pinned target materialization failed with status {Status}.", exception.Status);
            return false;
        }
        catch (Exception exception) when (project.RepositoryTarget is not null)
        {
            await executionReport!.UpdateAsync(
                project.RepositoryTarget.TargetId, TargetExecutionStatus.PipelineFailed,
                context.VerifiedBaseCommit, "Target pipeline failed.", "pipeline", exception.GetType().Name);
            project.Logger?.Error("Pinned target pipeline failed: {FailureKind}.", exception.GetType().Name);
            return false;
        }
        finally
        {
            if (_runMode == RunMode.CollectTests && workspacePrepared)
                await provider.GetRequiredService<IRepoOperations>().DeleteRepoAsync();
        }
    }

    private ServiceProvider BuildProjectServiceProvider(ProjectContext context)
    {
        var services = new ServiceCollection();
        services.AddTestMapServices(ConfigurationService, _config, context);
        return services.BuildServiceProvider();
    }
}
