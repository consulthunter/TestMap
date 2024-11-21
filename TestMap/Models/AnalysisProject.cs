/*
 * consulthunter
 * 2024-11-07
 * AnalysisProject is an abstraction of the current
 * (.csproj) that we are analyzing
 * AnalysisProject.cs
 */

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TestMap.Models;

/// <summary>
/// AnalysisProject
/// Representation of a single csharp project (.csproj)
/// from the repository
/// </summary>
/// <param name="solutionFilePath">Absolute filepath to the solution (.sln) file</param>
/// <param name="syntaxTrees">Syntax trees for source code files (.cs) loaded with the project</param>
/// <param name="projectReferences">References contained in the project to other project files (.csproj)</param>
/// <param name="assemblies">Assemblies loaded with the project</param>
/// <param name="projectFilePath">Absolute filepath to the project file (.csproj)</param>
/// <param name="compilation">Compilation loaded for the project</param>
/// <param name="languageFramework">Target framework found within the project file</param>
public class AnalysisProject(
    string? solutionFilePath = null,
    Dictionary<string, SyntaxTree>? syntaxTrees = null,
    List<string>? projectReferences = null,
    List<MetadataReference>? assemblies = null,
    string projectFilePath = "",
    CSharpCompilation? compilation = null,
    string languageFramework = "")
{
    public readonly string? SolutionFilePath = solutionFilePath;
    public readonly string ProjectFilePath = projectFilePath;
    public readonly Dictionary<string, SyntaxTree>? SyntaxTrees = syntaxTrees;
    public readonly List<string>? ProjectReferences = projectReferences;
    public readonly List<MetadataReference>? Assemblies = assemblies;
    public readonly CSharpCompilation? Compilation = compilation;
    public readonly string LanguageFramework = languageFramework;
}