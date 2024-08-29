using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestMap.Models;

namespace TestMap.Tests;

public class TestConfigurationLoader
{
    // fields
    private JObject _settings;
    private readonly string _configurationFilePath = @"F:\Projects\TestMap\TestMap.Tests\Config\config.json";
    private readonly string _targetFilePath;
    public readonly string LogsDirPath;
    private readonly string _tempDirPath;
    private readonly string _outputDirPath;
    public readonly int MaxConcurrency;
    public string RunDate;
    public ProjectModel ProjectModel = new();
    // methods
    public TestConfigurationLoader()
    {
        LoadConfiguration();
        // Read settings from configuration file
        _targetFilePath = _settings["FilePaths"]["TargetFilePath"].ToString();
        LogsDirPath = _settings["FilePaths"]["LogsDirPath"].ToString();
        _tempDirPath = _settings["FilePaths"]["TempDirPath"].ToString();
        _outputDirPath = _settings["FilePaths"]["OutputDirPath"].ToString();
        MaxConcurrency = int.Parse(_settings["Settings"]["MaxConcurrency"].ToString());
        RunDate = DateTime.UtcNow.ToString(_settings["Settings"]["RunDateFormat"].ToString());
        ConfigureRun();
    }

    private void ConfigureRun()
    {
        EnsureRunLogsDirectory();
        EnsureTempDirectory();
        EnsureRunOutputDirectory();
        ReadTarget();
    }

    private void LoadConfiguration()
    {
        var json = string.Empty;
        // Load the configuration from JSON file
        if (string.IsNullOrEmpty(_configurationFilePath) || !File.Exists(_configurationFilePath))
        {
            throw new FileNotFoundException($"Config file not found: {_configurationFilePath}");
        }
        try
        {
            json = File.ReadAllText(_configurationFilePath);
            _settings = JObject.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Failed to parse config file as JSON.", ex);
        }
    }
    private void ReadTarget()
    {
        if (Path.Exists(_targetFilePath))
        {
            // Open the file for reading using StreamReader
            using (StreamReader sr = new StreamReader(_targetFilePath))
            {
                string? line;

                // Read and display lines from the file until the end of the file is reached
                while ((line = sr.ReadLine()) != null)
                {
                    InitializeProjectModel(line);
                    break;
                }
            }
        }
    }
    private void InitializeProjectModel(string projectUrl)
    {
        string githubUrl = projectUrl;
        (string owner, string repoName) = ExtractOwnerAndRepo(githubUrl);
        string directoryPath = Path.Combine(_tempDirPath, repoName);

        ProjectModel projectModel = new ProjectModel(gitHubUrl: githubUrl, owner: owner, repoName: repoName,
            directoryPath: directoryPath, tempDirPath: _tempDirPath);
        
        EnsureProjectLogDir(projectModel);
        EnsureProjectOutputDir(projectModel);

        AnalysisProject analysisProject = CreateAnalysisProject();
        projectModel.Projects.Add(analysisProject);
        
        ProjectModel = projectModel;
    }
    private AnalysisProject CreateAnalysisProject()
    {
        var syntaxTrees = new Dictionary<string, SyntaxTree>
        {
            { "tree1", SyntaxFactory.ParseSyntaxTree("class C { }") }
        };
        var projectReferences = new List<string> { "Reference1", "Reference2" };
        var assemblies = new List<MetadataReference> { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var documents = new List<string> { "Doc1", "Doc2" };
        var projectFilePath = "path/to/project.csproj";

        return new AnalysisProject(syntaxTrees, projectReferences, assemblies, documents, projectFilePath);
    }
    private void EnsureRunLogsDirectory()
    {
        // Check if Logs directory exists, create if not
        if (!Directory.Exists(LogsDirPath))
        {
            Directory.CreateDirectory(LogsDirPath);

            if (!Directory.Exists(Path.Combine(LogsDirPath, RunDate)))
            {
                Directory.CreateDirectory(Path.Combine(LogsDirPath, RunDate));
            }
        }
    }
    
    private void EnsureTempDirectory()
    {
        // Check if Temp directory exists, create if not
        if (!Directory.Exists(_tempDirPath))
        {
            Directory.CreateDirectory(_tempDirPath);
            
        }
    }
    
    private void EnsureRunOutputDirectory()
    {
        // Check if Output directory exists, create if not
        if (!Directory.Exists(_outputDirPath))
        {
            Directory.CreateDirectory(_outputDirPath);
            
            if (!Directory.Exists(Path.Combine(_outputDirPath, RunDate)))
            {
                Directory.CreateDirectory(Path.Combine(_outputDirPath, RunDate));
            }
        }
    }
    public (string, string) ExtractOwnerAndRepo(string url)
    {

        if (url.StartsWith("https://"))
        {
            // HTTP(S) URL format: https://github.com/owner/repoName
            return ExtractOwnerAndRepoFromHttpUrl(url);
        }
        else
        {
            throw new ArgumentException("Unsupported URL format");
        }
    }
    private (string, string) ExtractOwnerAndRepoFromHttpUrl(string url)
    {
        Uri uri = new Uri(url);
        string owner = uri.Segments[1].TrimEnd('/');
        string repoName = uri.Segments[2].TrimEnd('/');

        return (owner, repoName);
    }

    private void EnsureProjectLogDir(ProjectModel project)
    {
        string logDirPath = Path.Combine(LogsDirPath, RunDate, project.ProjectId);

        if (!Directory.Exists(logDirPath))
        {
            Directory.CreateDirectory(logDirPath);
        }
        project.CreateLog(logDirPath);
    }

    private void EnsureProjectOutputDir(ProjectModel project)
    {
        string outputPath = Path.Combine(_outputDirPath, RunDate, project.ProjectId);
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }
        project.OutputPath = outputPath;
    }
}