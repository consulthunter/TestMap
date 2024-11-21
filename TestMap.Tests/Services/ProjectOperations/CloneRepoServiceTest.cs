using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Serilog;
using TestMap.Models;
using TestMap.Services;
using TestMap.Services.ProjectOperations;
using Xunit;

namespace TestMap.Tests.Services.ProjectOperations;

[TestSubject(typeof(CloneRepoService))]
public class CloneRepoServiceTest
{
    private readonly Mock<ProjectModel> _projectModelMock;
    private readonly CloneRepoService _service;

    public CloneRepoServiceTest()
    {
        var gitHubUrl = "https://github.com/example/repo";
        var owner = "owner";
        var repoName = "repo";
        var directoryPath = "path/to/dir";
        var tempDirPath = "path/to/temp";
        
        _projectModelMock = new Mock<ProjectModel>(MockBehavior.Strict, gitHubUrl, owner, repoName, directoryPath, tempDirPath);
        _service = new CloneRepoService(_projectModelMock.Object);
    }

    [Fact]
    public async Task CloneRepoAsync_DirectoryExists_Success()
    {
        // arrange
        await _service.CloneRepoAsync();

        // Assert
        _projectModelMock.Verify();
    }
}