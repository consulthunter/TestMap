/*
 * consulthunter
 * 2024-11-07
 * Structure for the CSV output
 * for Test Classes & Source Classes
 * TestClassRecord.cs
 */

namespace TestMap.Models;

/// <summary>
///     TestClassRecord
///     A single record (row) in the resulting CSV
///     for a test class and the source code class (class being tested)
/// </summary>
/// <param name="owner">Name of the account that owns the repo</param>
/// <param name="repo">Name of the repo</param>
/// <param name="solutionFilePath">Absolute filepath to the solution (.sln) file</param>
/// <param name="projectPath">Absolute filepath to the project (.csproj) file</param>
/// <param name="filePath">Absolute filepath to the code (.cs) file containing the test class</param>
/// <param name="ns">Namespace for the test class</param>
/// <param name="classDeclaration">Identifier from the ClassDeclarationSyntax</param>
/// <param name="classFields">Field declared in the test class</param>
/// <param name="usingStatements">Using statements defined in the test class file</param>
/// <param name="testFramework">Testing framework(s) used in the test class</param>
/// <param name="languageFramework">Version of .NET, if defined</param>
/// <param name="classBody">Body of the test class</param>
/// <param name="bodyStartPosition">Position in the syntax treee that marks the beginning of the Class body</param>
/// <param name="bodyEndPosition">Position in the syntax tree that marks the end of the Class body</param>
/// <param name="sourceBody">Corresponding source code class for the test code class, ex. Student.cs for StudentTest.cs</param>
public class TestClassRecord(
    string owner,
    string repo,
    string? solutionFilePath,
    string projectPath,
    string filePath,
    string ns,
    string classDeclaration,
    string classFields,
    string usingStatements,
    string testFramework,
    string languageFramework,
    string classBody,
    string bodyStartPosition,
    string bodyEndPosition,
    string sourceBody)
{
    public string Owner { get; set; } = owner;
    public string Repo { get; set; } = repo;
    public string? SolutionFilePath { get; set; } = solutionFilePath;
    public string ProjectPath { get; set; } = projectPath;
    public string FilePath { get; set; } = filePath;
    public string Namespace { get; set; } = ns;
    public string ClassDeclaration { get; set; } = classDeclaration;
    public string ClassFields { get; set; } = classFields;
    public string UsingStatements { get; set; } = usingStatements;
    public string TestFramework { get; set; } = testFramework;
    public string LanguageFramework { get; set; } = languageFramework;
    public string ClassBody { get; set; } = classBody;
    public string BodyStartPosition { get; set; } = bodyStartPosition;
    public string BodyEndPosition { get; set; } = bodyEndPosition;
    public string SourceBody { get; set; } = sourceBody;
}