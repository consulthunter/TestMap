using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using Serilog;
using TestMap.Models;
using Xunit;

namespace TestMap.Tests.Models;

[TestSubject(typeof(ProjectModel))]

public class ProjectModelTest
{
    private readonly string _githubUrl;
    private readonly string _owner;
    private readonly string _repoName;
    private readonly string _directoryPath;
    private readonly string _tempDirectoryPath;
    private readonly string _outputDirectoryPath;
    private readonly string _logDirectoryPath;
    private readonly Dictionary<string, List<string>> _testingFrameworks;
    private readonly Dictionary<string, string> _scripts;
    private readonly string _runDate;

    public ProjectModelTest()
    {
        _githubUrl = "https://github.com/owner/reponame";
        _owner = "owner";
        _repoName = "repo";
        _runDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        _tempDirectoryPath = Path.Combine("path", "to", "temp");
        _outputDirectoryPath = Path.Combine("path", "to", "output");
        _logDirectoryPath = Path.Combine("path", "to", "log");
        _directoryPath = Path.Combine(_tempDirectoryPath, _repoName);
        _testingFrameworks = new Dictionary<string, List<string>>()
        {
            { "xUnit", ["[Fact]"] }
        };
        _scripts = new Dictionary<string, string>()
        {
            {"Delete", "delete.bat" }
        };
    }

    private ProjectModel CreateProjectModel()
    {
        return new ProjectModel(_githubUrl, _owner, _repoName, _runDate, _directoryPath, _logDirectoryPath,
            _outputDirectoryPath, _tempDirectoryPath, _testingFrameworks, _scripts);
    }

    [Fact]
    public void Constructor_ShouldInitializeProjectModel()
    {
        var projectModel = CreateProjectModel();
        
        Assert.NotNull(projectModel.ProjectId);
        Assert.Equal(_githubUrl, projectModel.GitHubUrl);
        Assert.Equal(_owner, projectModel.Owner);
        Assert.Equal(_repoName, projectModel.RepoName);
        Assert.Equal(_directoryPath, projectModel.DirectoryPath);
        Assert.Equal(_tempDirectoryPath, projectModel.TempDirPath);
        Assert.Equal(_testingFrameworks, projectModel.TestingFrameworks);
        Assert.Equal(_scripts, projectModel.Scripts);
    }
}
