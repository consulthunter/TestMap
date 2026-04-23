using TestMap.Models.Code;

namespace TestMap.Services.StaticAnalysis;

public interface IAnalyzeProjectService
{
    Task AnalyzeProjectAsync(CSharpProjectModel analysisProject);
}
