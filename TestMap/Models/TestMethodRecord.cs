/*
 * consulthunter
 * 2024-11-07
 * Structure for the CSV output
 * for Test Methods & Invocation definitions
 * TestMethodRecord.cs
 */

namespace TestMap.Models;

/// <summary>
///     TestMethodRecord
///     A single record (row) in the resulting CSV
///     for test methods and invocations + definitions (methods called in the test
///     , plus their definitions)
/// </summary>
/// <param name="owner">Name of the account that owns the repo</param>
/// <param name="repo">Name of the repo</param>
/// <param name="solutionFilePath">Absolute filepath to the solution (.sln) file</param>
/// <param name="projectFilePath">Absolute filepath to the project (.csproj) file</param>
/// <param name="filePath">Absolute filepath to the code (.cs) file containing the test class</param>
/// <param name="ns">Namespace for the test class</param>
/// <param name="classDeclaration">Identifier from the ClassDeclarationSyntax</param>
/// <param name="classFields">Field declared in the test class</param>
/// <param name="usingStatements">Using statements defined in the test class file</param>
/// <param name="testFramework">Testing framework(s) used in the test class</param>
/// <param name="languageFramework">Version of .NET, if defined</param>
/// <param name="methodBody">Body of the test method</param>
/// <param name="bodyStartPosition">Position in the syntax tree marking the beginning of the test method</param>
/// <param name="bodyEndPosition">Position in the syntax tree marking the end of the test method</param>
/// <param name="methodInvocations">Methods called within the test method and the definition for that method</param>
public class TestMethodRecord(
    string owner,
    string repo,
    string? solutionFilePath,
    string projectFilePath,
    string filePath,
    string ns,
    string classDeclaration,
    string classFields,
    string usingStatements,
    string testFramework,
    string languageFramework,
    string methodBody,
    string bodyStartPosition,
    string bodyEndPosition,
    string methodInvocations)
{
    public string Owner { get; set; } = owner;
    public string Repo { get; set; } = repo;
    public string? SolutionFilePath { get; set; } = solutionFilePath;
    public string ProjectFilePath { get; set; } = projectFilePath;
    public string FilePath { get; set; } = filePath;
    public string Namespace { get; set; } = ns;
    public string ClassDeclaration { get; set; } = classDeclaration;
    public string ClassFields { get; set; } = classFields;
    public string UsingStatements { get; set; } = usingStatements;
    public string TestFramework { get; set; } = testFramework;
    public string LanguageFramework { get; set; } = languageFramework;
    public string MethodBody { get; set; } = methodBody;
    public string BodyStartPosition { get; set; } = bodyStartPosition;
    public string BodyEndPosition { get; set; } = bodyEndPosition;
    public string MethodInvocations { get; set; } = methodInvocations;
}