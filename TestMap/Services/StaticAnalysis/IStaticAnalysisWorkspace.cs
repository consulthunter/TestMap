using Microsoft.CodeAnalysis;

namespace TestMap.Services.StaticAnalysis;

public interface IStaticAnalysisWorkspace
{
    IReadOnlyList<string> WorkspaceFailures { get; }

    void ClearWorkspaceFailures();

    Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default);
    Task<Project> OpenProjectAsync(string projectPath, CancellationToken cancellationToken = default);
    Task<Project> RefreshProjectAsync(string projectPath, CancellationToken cancellationToken = default);
}
