using Microsoft.CodeAnalysis.CSharp;
using Serilog;
using TestMap.Services.ProjectOperations;

namespace TestMap.Models;

public class TestMap
{
    // fields
    public ProjectModel ProjectModel { get; private set; }
    private CloneRepoService CloneRepoService { get; set; }
    private BuildSolutionService BuildSolutionService { get; set; }
    private BuildProjectService BuildProjectService { get; set; }
    private AnalyzeProjectService AnalyzeProjectService { get; set; }
    private DeleteProjectService DeleteProjectService { get; set; }
    private SdkManager SdkManager { get; set; }
    public ILogger Logger { get; private set; }
    // methods
    public async Task RunAsync()
    {
        // await CloneProjectAsync();
        // await SDKManager

        await BuildSolutionAsync();
        await BuildProjectAsync();
        
        // Example: Delete project after processing
        // await DeleteProjectAsync();
    }

    private async Task CloneRepoAsync()
    {
        await CloneRepoService.CloneRepoAsync();
    }

    private async Task BuildSolutionAsync()
    {
        await BuildSolutionService.BuildSolutionsAsync();
    }

    private async Task BuildProjectAsync()
    {
        Logger.Information($"Number of projects in {ProjectModel.ProjectId}: {ProjectModel.Projects.Count}");
        // iterates over the project and loads project information
        foreach (var project in ProjectModel.Projects)
        {
            // assuming all project information is loaded
            // create project compilation
            CSharpCompilation cSharpCompilation = BuildProjectService.BuildProjectCompilation(project);
            // analyze the project
            await AnalyzeProjectAsync(project, cSharpCompilation);
        }
    }

    private async Task AnalyzeProjectAsync(AnalysisProject analysisProject, CSharpCompilation cSharpCompilation)
    {
        await AnalyzeProjectService.AnalyzeProjectAsync(analysisProject, cSharpCompilation);
    }

    private async Task DeleteProjectAsync()
    {
        await DeleteProjectService.DeleteProjectAsync();
    }
    
    // constructor
    public TestMap(ProjectModel projectModel)
    {
        ProjectModel = projectModel;
        
        // Create services
        CloneRepoService = new CloneRepoService(this);
        SdkManager = new SdkManager(this);
        BuildSolutionService = new BuildSolutionService(this);
        BuildProjectService = new BuildProjectService(this);
        AnalyzeProjectService = new AnalyzeProjectService(this);
        DeleteProjectService = new DeleteProjectService(this);
        
        // logger
        Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(ProjectModel.LogsFilePath)
            .CreateLogger();
    }
}