using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moq;
using TestMap.Models;
using TestMap.Services.ProjectOperations;
using Xunit;

namespace TestMap.Tests.Models;

[TestSubject(typeof(TestMap.Models.TestMap))]
public class TestMapTest
{
    private readonly Mock<ProjectModel> _projectModelMock;
    private readonly Mock<CloneRepoService> _mockCloneRepoService;
    private readonly Mock<SdkManager> _mockSdkManager;
    private readonly Mock<BuildSolutionService> _mockBuildSolutionService;
    private readonly Mock<AnalyzeProjectService> _mockAnalyzeProjectService;
    private readonly Mock<DeleteProjectService> _mockDeleteProjectService;
    private TestMap.Models.TestMap _testMap;

    public TestMapTest()
    {
        var gitHubUrl = "https://github.com/example/repo";
        var owner = "owner";
        var repoName = "repo";
        var directoryPath = "path/to/dir";
        var tempDirPath = "path/to/temp";
        var outputDirPath = "path/to/output";

        // Initialize mocks
        _projectModelMock =
            new Mock<ProjectModel>(MockBehavior.Strict, gitHubUrl, owner, repoName, directoryPath, tempDirPath);
        _projectModelMock.Object.Projects.Add(CreateAnalysisProject());
        _projectModelMock.Object.OutputPath = outputDirPath;
        _mockCloneRepoService = new Mock<CloneRepoService>(MockBehavior.Strict, _projectModelMock.Object);
        _mockSdkManager = new Mock<SdkManager>(MockBehavior.Strict, _projectModelMock.Object);
        _mockBuildSolutionService = new Mock<BuildSolutionService>(MockBehavior.Strict, _projectModelMock.Object);
        _mockAnalyzeProjectService = new Mock<AnalyzeProjectService>(MockBehavior.Strict, _projectModelMock.Object);
        _mockDeleteProjectService = new Mock<DeleteProjectService>(MockBehavior.Strict, _projectModelMock.Object);

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
            _mockSdkManager.Object,
            _mockBuildSolutionService.Object,
            _mockAnalyzeProjectService.Object,
            _mockDeleteProjectService.Object
        );
    }

    [Fact]
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