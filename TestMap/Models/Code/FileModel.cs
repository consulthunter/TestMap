/*
 * consulthunter
 * 2025-03-26
 *
 * An Abstraction of a single code (.cs) file
 *
 * CodeModel.cs
 */

using System.Text.Json;

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
public class FileModel(
    List<string> usingStatements,
    int analysisProjectId = 0,
    string guid = "",
    string ns = "",
    string name = "",
    string language = "",
    string solutionFilePath = "",
    string projectPath = "",
    string filePath = "")
{
    public int Id { get; set; } = 0;
    public int AnalysisProjectId { get; set; } = analysisProjectId;
    public string Guid { get; set; } = guid;
    public string Namespace { get; set; } = ns;
    public string Name { get; set; } = name;
    public string Language { get; set; } = language;

    public string MetaData { get; set; } = JsonSerializer.Serialize(new Dictionary<string, string>
    {
        { "SolutionFilePath", solutionFilePath },
        { "ProjectFilePath", projectPath }
    });

    public string UsingStatements { get; set; } = JsonSerializer.Serialize(usingStatements ?? new List<string>());
    public string FilePath { get; set; } = filePath;
    public string ContentHash { get; set; } = Utilities.Utilities.ComputeSha256(filePath);
}