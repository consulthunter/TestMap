using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moq;
using TestMap.Models;
using TestMap.Services.ProjectOperations;
using Xunit;

namespace TestMap.Tests.UnitTests.Models;

[TestSubject(typeof(TestMap.Models.TestMap))]
public class TestMapTest
{
    private readonly Mock<AnalyzeProjectService> _mockAnalyzeProjectService;
    private readonly Mock<BuildSolutionService> _mockBuildSolutionService;
    private readonly Mock<CloneRepoService> _mockCloneRepoService;
    private readonly Mock<DeleteProjectService> _mockDeleteProjectService;
    private readonly Mock<ProjectModel> _projectModelMock;
    private TestMap.Models.TestMap _testMap;

    public TestMapTest()
    {
        var gitHubUrl = "https://github.com/consulthunter/TestMap-Example";
        var owner = "consulthunter";
        var repoName = "TestMap-Example";
        var runDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var directoryPath = "path/to/dir";
        var logDirectoryPath = "path/to/log";
        var tempDirPath = "path/to/temp";
        var outputDirPath = "path/to/output";
        var testingFrameworks = new Dictionary<string, List<string>>
        {
            { "xUnit", ["[Fact]"] }
        };
        var scripts = new Dictionary<string, string>
        {
            { "Delete", "delete.bat" }
        };

        // Initialize mocks
        _projectModelMock =
            new Mock<ProjectModel>(MockBehavior.Strict, gitHubUrl, owner, repoName, runDate, directoryPath,
                logDirectoryPath,
                outputDirPath, tempDirPath, testingFrameworks, scripts);
        _projectModelMock.Object.Projects.Add(CreateAnalysisProject());
        _projectModelMock.Object.EnsureProjectOutputDir();
        _projectModelMock.Object.EnsureProjectLogDir();
        _mockCloneRepoService = new Mock<CloneRepoService>(_projectModelMock.Object);
        _mockBuildSolutionService = new Mock<BuildSolutionService>(_projectModelMock.Object);
        _mockAnalyzeProjectService = new Mock<AnalyzeProjectService>(_projectModelMock.Object);
        _mockDeleteProjectService = new Mock<DeleteProjectService>(_projectModelMock.Object);

        CreateTestMap();
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

    private void CreateTestMap()
    {
        _testMap = new TestMap.Models.TestMap
        (
            _projectModelMock.Object,
            _mockCloneRepoService.Object,
            _mockBuildSolutionService.Object,
            _mockAnalyzeProjectService.Object,
            _mockDeleteProjectService.Object
        );
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task RunAsync_CallsExpectedMethods()
    {
        // Arrange
        _mockBuildSolutionService
            .Setup(service => service.BuildSolutionsAsync())
            .Returns(Task.CompletedTask)
            .Verifiable();

        _mockAnalyzeProjectService
            .Setup(service => service.AnalyzeProjectAsync(It.IsAny<AnalysisProject>(), It.IsAny<CSharpCompilation>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await _testMap.RunAsync();

        // Assert
        _mockBuildSolutionService.Verify();
        _mockAnalyzeProjectService.Verify();
    }
}