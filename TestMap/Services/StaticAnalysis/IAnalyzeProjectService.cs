using TestMap.Models.Code;

namespace TestMap.Services.StaticAnalysis;

public interface IAnalyzeProjectService
{
    /// <summary>
    /// Analyzes a single project. When analyzing multiple projects in a solution,
    /// pass the same <paramref name="sharedMemberIds"/> dictionary to every call so that
    /// cross-project symbol keys can be resolved to their persisted member IDs.
    /// Production projects must be analyzed before the test projects that reference them
    /// (topological order) so that member IDs are populated before they are looked up.
    /// </summary>
    Task AnalyzeProjectAsync(
        CSharpProjectModel analysisProject,
        Dictionary<string, int>? sharedMemberIds = null);
}