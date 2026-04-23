using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace TestMap.Services.StaticAnalysis;

public sealed class StaticAnalysisWorkspace : IStaticAnalysisWorkspace, IDisposable
{
    private readonly MSBuildWorkspace _workspace = MSBuildWorkspace.Create();
    private readonly Dictionary<string, Task<Solution>> _solutionsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<Microsoft.CodeAnalysis.Project>> _projectsByPath = new(StringComparer.OrdinalIgnoreCase);

    public Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(solutionPath);
        if (!_solutionsByPath.TryGetValue(normalizedPath, out var solutionTask))
        {
            solutionTask = OpenSanitizedSolutionAsync(normalizedPath, cancellationToken);
            _solutionsByPath[normalizedPath] = solutionTask;
        }

        return solutionTask;
    }

    public async Task<Microsoft.CodeAnalysis.Project> OpenProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var normalizedProjectPath = NormalizePath(projectPath);

        foreach (var solutionTask in _solutionsByPath.Values)
        {
            var cachedSolution = await solutionTask;
            var cachedProject = cachedSolution.Projects.FirstOrDefault(project =>
                string.Equals(project.FilePath, normalizedProjectPath, StringComparison.OrdinalIgnoreCase));
            if (cachedProject != null)
            {
                return cachedProject;
            }
        }

        if (!_projectsByPath.TryGetValue(normalizedProjectPath, out var projectTask))
        {
            projectTask = OpenSanitizedProjectAsync(normalizedProjectPath, cancellationToken);
            _projectsByPath[normalizedProjectPath] = projectTask;
        }

        return await projectTask;
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private async Task<Solution> OpenSanitizedSolutionAsync(string solutionPath, CancellationToken cancellationToken)
    {
        var solution = await _workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
        return RemoveAnalyzerReferences(solution);
    }

    private async Task<Microsoft.CodeAnalysis.Project> OpenSanitizedProjectAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var project = await _workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        var solution = RemoveAnalyzerReferences(project.Solution);
        return solution.GetProject(project.Id) ?? project;
    }

    private static Solution RemoveAnalyzerReferences(Solution solution)
    {
        foreach (var project in solution.Projects)
        {
            if (project.AnalyzerReferences.Any())
            {
                solution = solution.WithProjectAnalyzerReferences(project.Id, []);
            }
        }

        return solution;
    }
}
