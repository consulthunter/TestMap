using Microsoft.CodeAnalysis;
using TestMap.Models.Code;

namespace TestMap.Services.ProjectDiscovery;

public interface IProjectBuildAnalysisService
{
    Task<ProjectBuildMetadataModel> AnalyzeAsync(Project project);
}