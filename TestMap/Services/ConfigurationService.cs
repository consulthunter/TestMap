/*
 * consulthunter
 * 2024-11-07
 * Uses the config file
 * to set the filepaths
 * and other variables
 * for the current run
 * ConfigurationService.cs
 */

using Microsoft.Extensions.Configuration;
using TestMap.Models;

namespace TestMap.Services;

/// <summary>
/// ConfigurationService
/// Takes in the configuration parsed from the JSON
/// Configures variables for the run.
/// </summary>
/// <param name="configuration">Configuration parsed from the JSON file</param>
public class ConfigurationService(IConfiguration configuration) : IConfigurationService
{
    // fields
    private readonly string? _targetFilePath = configuration["FilePaths:TargetFilePath"];
    private readonly string? _logsDirPath = configuration["FilePaths:LogsDirPath"];
    private readonly string? _tempDirPath = configuration["FilePaths:TempDirPath"];
    private readonly string? _outputDirPath = configuration["FilePaths:OutputDirPath"];
    private readonly int _maxConcurrency = int.Parse(configuration["Settings:MaxConcurrency"] ?? string.Empty);
    private readonly string _runDate = DateTime.UtcNow.ToString(configuration["Settings:RunDateFormat"]);
    private readonly List<ProjectModel> _projectModels = new();
    private readonly Dictionary<string, List<string>>? _testingFrameworks = configuration.GetSection("Frameworks").Get<Dictionary<string, List<string>>>();
    private readonly Dictionary<string, string>? _scripts = configuration.GetSection("Scripts").Get<Dictionary<string, string>>();
    private readonly Dictionary<string, string>? _environmentVariables = configuration.GetSection("EnvironmentVariables").Get<Dictionary<string, string>>();
    
    public int GetConcurrency()
    {
        return _maxConcurrency;
    }

    public List<ProjectModel> GetProjectModels()
    {
        return _projectModels;
    }

    public string GetRunDate()
    {
        return _runDate;
    }

    public string? GetTempDirPath()
    {
        return _tempDirPath;
    }

    public string? GetLogsDirectory()
    {
        return _logsDirPath;
    }

    public Dictionary<string, string>? GetScripts()
    {
        return _scripts;
    }

    public Dictionary<string, string>? GetEnvironmentVariables()
    {
        return _environmentVariables;
    }

    /// <summary>
    /// Core function of the configuration service
    /// </summary>
    public async Task ConfigureRunAsync()
    {
        EnsureRunLogsDirectory();
        EnsureTempDirectory();
        EnsureRunOutputDirectory();
        await ReadTargetAsync();
    }

    /// <summary>
    /// Reads the file defined in the config, TargetFilePath
    /// </summary>
    private async Task ReadTargetAsync()
    {
        if (Path.Exists(_targetFilePath))
            // Open the file for reading using StreamReader
            using (var sr = new StreamReader(_targetFilePath))
            {
                string? line;

                // Read and display lines from the file until the end of the file is reached
                while ((line = await sr.ReadLineAsync()) != null) InitializeProjectModel(line);
            }
    }

    /// <summary>
    /// Creates a project model for each
    /// targeted repo defined in the text file
    /// for TargetFilePath
    /// </summary>
    /// <param name="projectUrl">Full URL from the list in target file</param>
    private void InitializeProjectModel(string projectUrl)
    {
        var githubUrl = projectUrl;
        (var owner, var repoName) = ExtractOwnerAndRepo(githubUrl);
        if (_tempDirPath != null)
        {
            var directoryPath = Path.Combine(_tempDirPath, repoName);

            var projectModel = new ProjectModel(githubUrl, owner, repoName, _runDate,
                directoryPath, _logsDirPath, _outputDirPath, _tempDirPath, _testingFrameworks, _scripts);

            _projectModels.Add(projectModel);
        }
    }

    /// <summary>
    /// Makes sure the Logs directory
    /// is present
    /// </summary>
    private void EnsureRunLogsDirectory()
    {
        // Check if Logs directory exists, create if not
        if (!Directory.Exists(_logsDirPath))
        {
            if (_logsDirPath != null)
            {
                Directory.CreateDirectory(_logsDirPath);

                if (!Directory.Exists(Path.Combine(_logsDirPath, _runDate)))
                    Directory.CreateDirectory(Path.Combine(_logsDirPath, _runDate));
            }
        }
    }

    /// <summary>
    /// Makes sure the Temp directory
    /// is present
    /// </summary>
    private void EnsureTempDirectory()
    {
        // Check if Temp directory exists, create if not
        if (!Directory.Exists(_tempDirPath))
            if (_tempDirPath != null)
                Directory.CreateDirectory(_tempDirPath);
    }

    /// <summary>
    /// Makes sure the Output directory
    /// is present
    /// </summary>
    private void EnsureRunOutputDirectory()
    {
        // Check if Output directory exists, create if not
        if (!Directory.Exists(_outputDirPath))
        {
            if (_outputDirPath != null)
            {
                Directory.CreateDirectory(_outputDirPath);

                if (!Directory.Exists(Path.Combine(_outputDirPath, _runDate)))
                    Directory.CreateDirectory(Path.Combine(_outputDirPath, _runDate));
            }
        }
    }

    /// <summary>
    /// Extracts the owner and repo name
    /// from the URL
    /// </summary>
    /// <param name="url">Full url for the targeted repo</param>
    /// <returns>Tuple: (owner, repo)</returns>
    /// <exception cref="ArgumentException">If the URL isn't in the expected form</exception>
    private (string, string) ExtractOwnerAndRepo(string url)
    {
        if (url.StartsWith("https://"))
            // HTTP(S) URL format: https://github.com/owner/repoName
            return ExtractOwnerAndRepoFromHttpUrl(url);
        else
            throw new ArgumentException("Unsupported URL format");
    }

    /// <summary>
    /// Parses the owner and repo from the URL
    /// </summary>
    /// <param name="url">Full url for the targeted repo</param>
    /// <returns>Tuple: (owner, repo)</returns>
    private (string, string) ExtractOwnerAndRepoFromHttpUrl(string url)
    {
        var uri = new Uri(url);
        var owner = uri.Segments[1].TrimEnd('/');
        var repoName = uri.Segments[2].TrimEnd('/');

        return (owner, repoName);
    }
}