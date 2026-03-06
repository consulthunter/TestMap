using Microsoft.CodeAnalysis.CSharp;
using TestMap.Models;

namespace TestMap.Services.StaticAnalysis;

public interface IAnalyzeProjectService
{
    Task AnalyzeProjectAsync(AnalysisProject analysisProject, CSharpCompilation? cSharpCompilation);
}