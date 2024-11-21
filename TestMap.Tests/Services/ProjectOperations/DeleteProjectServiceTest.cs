using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using TestMap.Models;
using TestMap.Services;
using TestMap.Services.ProjectOperations;
using Xunit;

namespace TestMap.Tests.Services.ProjectOperations;

[TestSubject(typeof(DeleteProjectService))]
public class DeleteProjectServiceTest
{
    private readonly Mock<ProjectModel> _projectModelMock;
    private readonly DeleteProjectService _service;

    public DeleteProjectServiceTest()
    {
        var gitHubUrl = "https://github.com/example/repo";
        var owner = "owner";
        var repoName = "repo";
        var directoryPath = "path/to/dir";
        var tempDirPath = "path/to/temp";
        
        _projectModelMock = new Mock<ProjectModel>(MockBehavior.Strict, gitHubUrl, owner, repoName, directoryPath, tempDirPath);
        _service = new DeleteProjectService(_projectModelMock.Object);
    }
    [Fact]
    public async Task DeleteProjectService_ProjectModelIsNotNull()
    {
        // arrange
        await _service.DeleteProjectAsync();

        // Assert
        _projectModelMock.Verify();
    }
}