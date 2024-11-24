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
using TestMap.Services.ProjectOperations;

namespace TestMap.Models;

/// <summary>
///     TestMap
///     Manages services and executions for a single project model
/// </summary>
/// <param name="projectModel">Structure for the repo</param>
/// <param name="cloneRepoService">Service to clone the repo</param>
/// <param name="sdkManager">NOT USED, ideally this would install SDKs to build the project</param>
/// <param name="buildSolutionService">Service to find, load the solutions, projects, syntax trees, etc.</param>
/// <param name="analyzeProjectService">Service to find tests and create the CSV</param>
/// <param name="deleteProjectService">Service to remove the repo from the Temp directory</param>
public class TestMap(
    ProjectModel projectModel,
    ICloneRepoService cloneRepoService,
    SdkManager sdkManager,
    IBuildSolutionService buildSolutionService,
    IAnalyzeProjectService analyzeProjectService,
    IDeleteProjectService deleteProjectService)
{
    // fields
    public ProjectModel ProjectModel { get; } = projectModel;
    private ICloneRepoService CloneRepoService { get; } = cloneRepoService;
    private IBuildSolutionService BuildSolutionService { get; } = buildSolutionService;
    private IAnalyzeProjectService AnalyzeProjectService { get; } = analyzeProjectService;
    private IDeleteProjectService DeleteProjectService { get; } = deleteProjectService;

    private SdkManager SdkManager { get; set; } = sdkManager;

    // methods
    public async Task RunAsync()
    {
        await CloneRepoAsync();
        await BuildSolutionAsync();
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
    private async Task BuildSolutionAsync()
    {
        await BuildSolutionService.BuildSolutionsAsync();
    }

    /// <summary>
    ///     Starts the analysis for each project
    ///     in the project model project list
    /// </summary>
    private async Task AnalyzeProjectsAsync()
    {
        try
        {
            ProjectModel.Logger.Information(
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
            ProjectModel.Logger.Error(e.Message);
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

    /// <summary>
    ///     Deletes the project from the Temp directory
    /// </summary>
    private async Task DeleteProjectAsync()
    {
        await DeleteProjectService.DeleteProjectAsync();
    }
}