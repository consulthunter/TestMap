using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moq;
using TestMap.Models;
using TestMap.Services.ProjectOperations;
using Xunit;

namespace TestMap.Tests.Services.ProjectOperations;

[TestSubject(typeof(AnalyzeProjectService))]
public class AnalyzeProjectServiceTest
{
    private readonly Mock<ProjectModel> _projectModelMock;
    private readonly AnalyzeProjectService _service;

    public AnalyzeProjectServiceTest()
    {
        var gitHubUrl = "https://github.com/example/repo";
        var owner = "owner";
        var repoName = "repo";
        var directoryPath = "path/to/dir";
        var tempDirPath = "path/to/temp";
        var outputDirPath = "path/to/output";
        
        _projectModelMock =
            new Mock<ProjectModel>(MockBehavior.Strict, gitHubUrl, owner, repoName, directoryPath, tempDirPath);
        _projectModelMock.Object.EnsureProjectOutputDir();
        _projectModelMock.Object.EnsureProjectLogDir();
        _projectModelMock.Object.Projects.Add(CreateAnalysisProject());
        _projectModelMock.Object.OutputPath = outputDirPath;
        _service = new AnalyzeProjectService(_projectModelMock.Object);
    }
    private AnalysisProject CreateAnalysisProject()
    {
        var solution = "solution.sln";
        var syntaxTrees = new Dictionary<string, SyntaxTree>
        {
            { "tree1", SyntaxFactory.ParseSyntaxTree("class C { }") }
        };
        var projectReferences = new List<string> { "Reference1", "Reference2" };
        var assemblies = new List<MetadataReference>
            { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var projectFilePath = "path/to/project.csproj";

        return new AnalysisProject(solution, syntaxTrees, projectReferences, assemblies, projectFilePath);
    }

    private CSharpCompilation CreateCSharpCompilation(AnalysisProject analysisProject)
    {
        var compilation = CSharpCompilation.Create("temp",
            analysisProject.SyntaxTrees.Values.ToList(),
            analysisProject.Assemblies,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        return compilation;
    }
    
    [Fact]
    public async Task AnalyzeProjectService_ProjectModelNotNull()
    {
        // Arrange
        var compilation = CreateCSharpCompilation(_projectModelMock.Object.Projects.First());
        
        // Act
        await _service.AnalyzeProjectAsync(_projectModelMock.Object.Projects.First(), compilation);

        // Assert
        _projectModelMock.Verify();
    }
}