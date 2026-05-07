using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace TestMap.Services.StaticAnalysis;

public sealed class StaticAnalysisWorkspace : IStaticAnalysisWorkspace, IDisposable
{
    private MSBuildWorkspace _workspace;
    private readonly Dictionary<string, Task<Solution>> _solutionsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<Project>> _projectsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _workspaceFailures = new();

    public StaticAnalysisWorkspace()
    {
        _workspace = CreateWorkspace();
    }

    public IReadOnlyList<string> WorkspaceFailures => _workspaceFailures;

    public void ClearWorkspaceFailures()
    {
        _workspaceFailures.Clear();
    }

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

    public async Task<Project> OpenProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var normalizedProjectPath = NormalizePath(projectPath);

        foreach (var solutionTask in _solutionsByPath.Values)
        {
            var cachedSolution = await solutionTask;
            var cachedProject = cachedSolution.Projects.FirstOrDefault(project =>
                string.Equals(project.FilePath, normalizedProjectPath, StringComparison.OrdinalIgnoreCase));
            if (cachedProject != null) return cachedProject;
        }

        if (!_projectsByPath.TryGetValue(normalizedProjectPath, out var projectTask))
        {
            projectTask = OpenSanitizedProjectAsync(normalizedProjectPath, cancellationToken);
            _projectsByPath[normalizedProjectPath] = projectTask;
        }

        return await projectTask;
    }

    public async Task<Project> RefreshProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ResetWorkspace();
        var normalizedProjectPath = NormalizePath(projectPath);
        var projectTask = OpenSanitizedProjectAsync(normalizedProjectPath, cancellationToken);
        _projectsByPath[normalizedProjectPath] = projectTask;
        return await projectTask;
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private MSBuildWorkspace CreateWorkspace()
    {
        var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            _workspaceFailures.Add($"{args.Diagnostic.Kind}: {args.Diagnostic.Message}");
        });

        return workspace;
    }

    private void ResetWorkspace()
    {
        _workspace.Dispose();
        _workspace = CreateWorkspace();
        _solutionsByPath.Clear();
        _projectsByPath.Clear();
        _workspaceFailures.Clear();
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private async Task<Solution> OpenSanitizedSolutionAsync(string solutionPath, CancellationToken cancellationToken)
    {
        var solution = await _workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
        return RemoveAnalyzerReferences(solution);
    }

    private async Task<Project> OpenSanitizedProjectAsync(
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
            if (project.AnalyzerReferences.Any())
                solution = solution.WithProjectAnalyzerReferences(project.Id, []);

        return solution;
    }
}
