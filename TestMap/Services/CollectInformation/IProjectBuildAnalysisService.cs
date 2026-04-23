using Microsoft.CodeAnalysis;
using TestMap.Models.Code;

namespace TestMap.Services.CollectInformation;

public interface IProjectBuildAnalysisService
{
    Task<ProjectBuildMetadataModel> AnalyzeAsync(Project project);
}
