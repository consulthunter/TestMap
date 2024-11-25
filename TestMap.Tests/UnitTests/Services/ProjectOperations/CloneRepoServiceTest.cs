using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using TestMap.Models;
using TestMap.Services.ProjectOperations;
using Xunit;

namespace TestMap.Tests.UnitTests.Services.ProjectOperations;

[TestSubject(typeof(CloneRepoService))]
public class CloneRepoServiceTest
{
    private readonly Mock<ProjectModel> _projectModelMock;
    private readonly CloneRepoService _service;

    public CloneRepoServiceTest()
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
        _projectModelMock.Object.EnsureProjectOutputDir();
        _projectModelMock.Object.EnsureProjectLogDir();

        _service = new CloneRepoService(_projectModelMock.Object);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task CloneRepoAsync_DirectoryExists_Success()
    {
        // arrange
        await _service.CloneRepoAsync();

        // Assert
        _projectModelMock.Verify();
    }
}