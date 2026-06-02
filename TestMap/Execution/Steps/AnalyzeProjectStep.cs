using TestMap.App;
using TestMap.Models.Code;
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

        // Shared dictionary accumulates symbol-key → DB member ID across all project analyses.
        // Combined with topological ordering below, this lets cross-project invocations
        // (test → production) be resolved without DB round trips.
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var project in TopologicallySorted(context.Project.Projects))
            try
            {
                if (!_analyzedProjectIds.Add(project.FilePath))
                {
                    context.Logger.Information($"Skipping already analyzed project: {project.FilePath}");
                    continue;
                }

                await _analyzeProjectService.AnalyzeProjectAsync(project, sharedMemberIds);
            }
            catch (Exception e)
            {
                context.Logger.Error(e, "Project analysis failed for {ProjectFilePath}", project.FilePath);
            }
    }

    /// <summary>
    /// Returns projects sorted so that every dependency comes before the projects that
    /// depend on it (dependencies-first topological order). Production projects are
    /// therefore always processed before the test projects that reference them.
    /// Projects with no matching dependencies in the input list are treated as roots.
    /// </summary>
    private static IEnumerable<CSharpProjectModel> TopologicallySorted(
        IReadOnlyList<CSharpProjectModel> projects)
    {
        var byPath = projects.ToDictionary(
            p => p.FilePath,
            p => p,
            StringComparer.OrdinalIgnoreCase);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CSharpProjectModel>(projects.Count);

        void Visit(CSharpProjectModel project)
        {
            if (!visited.Add(project.FilePath)) return;

            foreach (var refPath in project.ProjectReferences)
                if (byPath.TryGetValue(refPath, out var dependency))
                    Visit(dependency);

            result.Add(project);
        }

        foreach (var project in projects)
            Visit(project);

        return result;
    }
}