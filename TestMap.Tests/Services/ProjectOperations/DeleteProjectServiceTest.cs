using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using TestMap.Models;
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