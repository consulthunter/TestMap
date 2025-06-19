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
using TestMap.Services.ProjectOperations;

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
    ICloneRepoService cloneRepoService,
    IExtractInformationService extractInformationService,
    IBuildTestService buildTestService,
    IAnalyzeProjectService analyzeProjectService,
    IGenerateTestService generateTestService,
    IDeleteProjectService deleteProjectService,
    RunMode runMode)
{
    // fields
    public ProjectModel ProjectModel { get; } = projectModel;
    private ICloneRepoService CloneRepoService { get; } = cloneRepoService;
    private IExtractInformationService ExtractInformationService { get; } = extractInformationService;
    private IBuildTestService BuildTestService { get; } = buildTestService;
    private IAnalyzeProjectService AnalyzeProjectService { get; } = analyzeProjectService;
    private IGenerateTestService GenerateTestService { get; } = generateTestService;
    private IDeleteProjectService DeleteProjectService { get; } = deleteProjectService;
    
    private RunMode RunMode { get; } = runMode;

    // methods
    public async Task RunAsync()
    {
        switch (RunMode)
        {
            case RunMode.CollectTests:
                await CollectTestsModeAsync();
                break;
            case RunMode.GenerateTests:
                await GenerateTestsModeAsync();
                break;
        }
    }

    private async Task CollectTestsModeAsync()
    {
        await CloneRepoAsync();
        await ExtractInformationAsync();
        await BuildTestAsync();
        await AnalyzeProjectsAsync();
        await DeleteProjectAsync();
    }

    private async Task GenerateTestsModeAsync()
    {
        await CloneRepoAsync();
        await GenerateTestAsync();
        await ExtractInformationAsync();
        await BuildTestAsync();
        await AnalyzeProjectsAsync();
        await DeleteProjectAsync();
    }

    private async Task FullAnalysisModeAsync()
    {
        await CloneRepoAsync();
        await ExtractInformationAsync();
        await BuildTestAsync();
        await AnalyzeProjectsAsync();
        await GenerateTestAsync();
        await ExtractInformationAsync();
        await BuildTestAsync();
        await AnalyzeProjectsAsync();
        await DeleteProjectAsync();
    }

    /// <summary>
    ///     Uses LibGit2Sharp to clone the repo to
    ///     the Temp directory
    /// </summary>
    private async Task CloneRepoAsync()
    {
        await CloneRepoService.CloneRepoAsync();
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
        await BuildTestService.BuildTestAsync();
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
            // iterates over the project and loads project information
            foreach (var project in ProjectModel.Projects)
                // assuming all project information is loaded
                // create project compilation
                // BuildProjectService.BuildProjectCompilation(project);
                // analyze the project
                await AnalyzeProjectAsync(project, project.Compilation);
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