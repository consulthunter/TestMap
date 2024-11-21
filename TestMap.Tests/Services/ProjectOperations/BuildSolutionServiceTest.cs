using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using TestMap.Models;
using TestMap.Services.ProjectOperations;
using Xunit;

namespace TestMap.Tests.Services.ProjectOperations;

[TestSubject(typeof(BuildSolutionService))]
public class BuildSolutionServiceTest
{
    private readonly Mock<ProjectModel> _projectModelMock;
    private readonly BuildSolutionService _service;

    public BuildSolutionServiceTest()
    {
        var gitHubUrl = "https://github.com/example/repo";
        var owner = "owner";
        var repoName = "repo";
        var directoryPath = "path/to/dir";
        var tempDirPath = "path/to/temp";
        
        _projectModelMock =
            new Mock<ProjectModel>(MockBehavior.Strict, gitHubUrl, owner, repoName, directoryPath, tempDirPath);
        _service = new BuildSolutionService(_projectModelMock.Object);
    }
    [Fact]
    public async Task BuildSolutionService_BuildsSolution_Project()
    {
        await _service.BuildSolutionsAsync();
        
        // assert
        _projectModelMock.Verify();
    }
}