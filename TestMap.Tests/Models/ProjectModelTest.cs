using System.IO;
using JetBrains.Annotations;
using TestMap.Models;
using Xunit;

namespace TestMap.Tests.Models;

[TestSubject(typeof(ProjectModel))]
public class ProjectModelTest
{

    [Fact]
    public void Constructor_ShouldInitializeFieldsCorrectly()
    {
        // Arrange
        var gitHubUrl = "https://github.com/example/repo";
        var owner = "owner";
        var repoName = "repo";
        var directoryPath = "path/to/dir";
        var tempDirPath = "path/to/temp";

        // Act
        var projectModel = new ProjectModel(gitHubUrl, owner, repoName, directoryPath, tempDirPath);

        // Assert
        Assert.Equal(gitHubUrl, projectModel.GitHubUrl);
        Assert.Equal(owner, projectModel.Owner);
        Assert.Equal(repoName, projectModel.RepoName);
        Assert.Equal(directoryPath, projectModel.DirectoryPath);
        Assert.Equal(tempDirPath, projectModel.TempDirPath);
        Assert.NotNull(projectModel.Solutions);
        Assert.Empty(projectModel.Solutions); // Default to empty list
        Assert.NotNull(projectModel.Projects);
        Assert.Empty(projectModel.Projects); // Default to empty list

        // Verify ProjectId was set with a random number and repo name
        Assert.Matches(@"^\d{1,2}_repo$", projectModel.ProjectId); // Adjust regex based on your ProjectId pattern
    }

    [Fact]
    public void SetLogFilePath_ShouldSetLogsFilePathCorrectly()
    {
        // Arrange
        var projectModel = new ProjectModel("https://github.com/example/repo", "owner", "repo");
        var logDirPath = "logs";
        var projectId = "123_repo";
        projectModel.ProjectId = projectId; // Set ProjectId to a known value

        // Act
        projectModel.CreateLog(logDirPath);

        // Assert
        var expectedLogFilePath = Path.Combine(logDirPath, $"{projectId}.log");
        Assert.Equal(expectedLogFilePath, projectModel.LogsFilePath);
    }
}