/*
 * consulthunter
 * 2024-11-07
 * An abstraction for the program
 * Each repository is a single project model
 * Each TestMap contains a single project model
 * and the services for that project model
 * TestMap.cs
 */

using Microsoft.CodeAnalysis.CSharp;
using TestMap.Models.Configuration;
using TestMap.Services.CollectInformation;
using TestMap.Services.Database;
using TestMap.Services.ProjectOperations;
using TestMap.Services.Testing;

namespace TestMap.Models;

/// <summary>
///     TestMap
///     Manages services and executions for a single project model
/// </summary>
/// <param name="projectModel">Structure for the repo</param>
/// <param name="cloneRepoService">Service to clone the repo</param>
/// <param name="extractInformationService">Service to find, load the solutions, projects, syntax trees, etc.</param>
/// <param name="analyzeProjectService">Service to find tests and create the CSV</param>
/// <param name="deleteProjectService">Service to remove the repo from the Temp directory</param>
public class TestMap(
    ProjectModel projectModel,
    TestMapConfig config,
    ICloneRepoService cloneRepoService,
    IExtractInformationService extractInformationService,
    IBuildTestService buildTestService,
    ISqliteDatabaseService sqliteDatabaseService,
    IAnalyzeProjectService analyzeProjectService,
    IMapUnresolvedService mapUnresolvedService,
    IGenerateTestService generateTestService,
    IDeleteProjectService deleteProjectService,
    RunMode runMode)
{
    // fields
    public ProjectModel ProjectModel { get; } = projectModel;
    private TestMapConfig Config { get; } = config;
    private ICloneRepoService CloneRepoService { get; } = cloneRepoService;
    private IExtractInformationService ExtractInformationService { get; } = extractInformationService;
    private IBuildTestService BuildTestService { get; } = buildTestService;
    private ISqliteDatabaseService SqliteDatabaseService { get; } = sqliteDatabaseService;
    private IAnalyzeProjectService AnalyzeProjectService { get; } = analyzeProjectService;
    private IMapUnresolvedService MapUnresolvedService { get; } = mapUnresolvedService;
    private IGenerateTestService GenerateTestService { get; } = generateTestService;
    private IDeleteProjectService DeleteProjectService { get; } = deleteProjectService;
    private readonly HashSet<string> _analyzedProjectIds = new();


    private RunMode RunMode { get; } = runMode;

    // methods
    public async Task RunAsync()
    {
        await CloneRepoAsync();
        await LoadDatabaseAsync();
        switch (RunMode)
        {
            case RunMode.CollectTests:
                await CollectTestsModeAsync();
                break;
            case RunMode.GenerateTests:
                await GenerateTestsModeAsync();
                break;
        }

        if (!Config.Persistence.KeepProjectFiles) await DeleteProjectAsync();
    }

    private async Task CollectTestsModeAsync()
    {
        await ExtractInformationAsync();
        await InsertProjectionInformation();
        await AnalyzeProjectsAsync();
        await BuildTestAsync();
        await MapUnresolvedServiceAsync();
    }

    private async Task GenerateTestsModeAsync()
    {
        await GenerateTestAsync();
    }

    /// <summary>
    ///     Uses LibGit2Sharp to clone the repo to
    ///     the Temp directory
    /// </summary>
    private async Task CloneRepoAsync()
    {
        await CloneRepoService.CloneRepoAsync();
    }

    private async Task LoadDatabaseAsync()
    {
        await SqliteDatabaseService.InitializeAsync();
    }

    private async Task InsertProjectionInformation()
    {
        await SqliteDatabaseService.InsertProjectInformation();
    }

    /// <summary>
    ///     Finds the solutions (.sln) in the repo
    ///     And loads the projects (.csproj) for each
    ///     solution in the repo
    /// </summary>
    private async Task ExtractInformationAsync()
    {
        await ExtractInformationService.ExtractInfoAsync();
    }

    private async Task BuildTestAsync()
    {
        // run baseline for all solutions
        var sols = ProjectModel.Solutions
            .Select(x => x.SolutionFilePath)
            .ToList();

        await BuildTestService.BuildTestAsync(sols, true);
    }

    /// <summary>
    ///     Starts the analysis for each project
    ///     in the project model project list
    /// </summary>
    private async Task AnalyzeProjectsAsync()
    {
        try
        {
            ProjectModel.Logger?.Information(
                $"Number of projects in {ProjectModel.ProjectId}: {ProjectModel.Projects.Count}");

            foreach (var project in ProjectModel.Projects)
            {
                if (!_analyzedProjectIds.Add(project.ProjectFilePath))
                {
                    ProjectModel.Logger?.Information($"Skipping already analyzed project: {project.ProjectFilePath}");
                    continue;
                }

                // Mark as analyzed before or after to avoid double work in concurrency

                await AnalyzeProjectAsync(project, project.Compilation);
            }
        }
        catch (Exception e)
        {
            ProjectModel.Logger?.Error(e.Message);
        }
    }

    /// <summary>
    ///     Analyzes and creates the output for the repository
    /// </summary>
    /// <param name="analysisProject">Analysis project for the (.csproj)</param>
    /// <param name="cSharpCompilation">Compilation for the (.csproj)</param>
    private async Task AnalyzeProjectAsync(AnalysisProject analysisProject, CSharpCompilation? cSharpCompilation)
    {
        await AnalyzeProjectService.AnalyzeProjectAsync(analysisProject, cSharpCompilation);
    }

    private async Task MapUnresolvedServiceAsync()
    {
        await MapUnresolvedService.MapUnresolvedAsync();
    }

    private async Task GenerateTestAsync()
    {
        await GenerateTestService.GenerateTestAsync();
    }

    /// <summary>
    ///     Deletes the project from the Temp directory
    /// </summary>
    private async Task DeleteProjectAsync()
    {
        await DeleteProjectService.DeleteProjectAsync();
    }
}