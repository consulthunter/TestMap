using TestMap.App;
using TestMap.Services.StaticAnalysis;

namespace TestMap.Execution.Steps;

public class AnalyzeProjectStep : IPipelineStep
{
    private readonly IAnalyzeProjectService _analyzeProjectService;
    private readonly HashSet<string> _analyzedProjectIds = new();

    public AnalyzeProjectStep(IAnalyzeProjectService analyzeProjectService)
    {
        _analyzeProjectService = analyzeProjectService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        if (context == null) return;

        context.Logger.Information(
            $"Number of projects in {context.Project.ProjectId}: {context.Project.Projects.Count}");

        foreach (var project in context.Project.Projects)
            try
            {
                if (!_analyzedProjectIds.Add(project.FilePath))
                {
                    context.Logger.Information($"Skipping already analyzed project: {project.FilePath}");
                    continue;
                }

                // Mark as analyzed before or after to avoid double work in concurrency
                await _analyzeProjectService.AnalyzeProjectAsync(project);
            }
            catch (Exception e)
            {
                context.Logger.Error(e, "Project analysis failed for {ProjectFilePath}", project.FilePath);
            }
    }
}