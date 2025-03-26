/*
 * consulthunter
 * 2025-03-26
 *
 * An Abstraction of a single code (.cs) file
 *
 * CodeModel.cs
 */

namespace TestMap.Models.Code;

/// <summary>
///     Representation of a single code (.cs) file
/// </summary>
/// <param name="owner">Owner of the repo</param>
/// <param name="repo">Name of the repo</param>
/// <param name="solutionFilePath">Path to solution (.sln) containing the file</param>
/// <param name="projectPath">Path to project (.csproj) containing the file</param>
/// <param name="filePath">Path to the code (.cs) file</param>
/// <param name="ns">Namespace for the file</param>
/// <param name="usingStatements">Using statements for the file</param>
/// <param name="languageFramework">Targeted language framework for the project/repo</param>
public class CodeModel(
    string owner,
    string repo,
    string solutionFilePath,
    string projectPath,
    string filePath,
    string ns,
    List<string> usingStatements,
    string languageFramework)
{
    public string Owner { get; set; } = owner;
    public string Repo { get; set; } = repo;
    public string? SolutionFilePath { get; set; } = solutionFilePath;
    public string ProjectPath { get; set; } = projectPath;
    public string FilePath { get; set; } = filePath;
    public string Namespace { get; set; } = ns;
    public List<string> UsingStatements { get; set; } = usingStatements;
    public string LanguageFramework { get; set; } = languageFramework;
    
    public List<TestClass> TestClasses { get; set; } = new List<TestClass>();
    
}