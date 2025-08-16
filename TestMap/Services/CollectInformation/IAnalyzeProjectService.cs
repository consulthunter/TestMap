using Microsoft.CodeAnalysis.CSharp;
using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public interface IAnalyzeProjectService
{
    Task AnalyzeProjectAsync(AnalysisProject analysisProject, CSharpCompilation? cSharpCompilation);
}