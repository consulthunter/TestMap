using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using TestMap.Models;
using TestMap.Services.ProjectOperations;
using Xunit;

namespace TestMap.Tests.UnitTests.Services.ProjectOperations;

[TestSubject(typeof(BuildSolutionService))]
public class BuildSolutionServiceTest
{
    private readonly Mock<ProjectModel> _projectModelMock;
    private readonly BuildSolutionService _service;

    public BuildSolutionServiceTest()
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

        _service = new BuildSolutionService(_projectModelMock.Object);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task BuildSolutionService_BuildsSolution_Project()
    {
        await _service.BuildSolutionsAsync();

        // assert
        _projectModelMock.Verify();
    }
}