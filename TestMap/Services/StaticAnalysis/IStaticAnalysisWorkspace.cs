using Microsoft.CodeAnalysis;

namespace TestMap.Services.StaticAnalysis;

public interface IStaticAnalysisWorkspace
{
    Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default);
    Task<Project> OpenProjectAsync(string projectPath, CancellationToken cancellationToken = default);
}