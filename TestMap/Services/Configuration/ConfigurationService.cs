/*
 * consulthunter
 * 2024-11-07
 * Uses the config file
 * to set the filepaths
 * and other variables
 * for the current run
 * ConfigurationService.cs
 */

using TestMap.Models;
using TestMap.Models.Configuration;

namespace TestMap.Services.Configuration;

/// <summary>
///     ConfigurationService
///     Takes in the configuration parsed from the JSON
///     Configures variables for the run.
/// </summary>
/// <param name="configuration">Configuration parsed from the JSON file</param>
public class ConfigurationService(TestMapConfig config) : IConfigurationService
{
    private readonly TestMapConfig _config = config;
    
    private readonly List<ProjectModel> _projectModels = new();
    private readonly string _runDate = DateTime.UtcNow.ToString(config.Settings.RunDateFormat);
    
    public int GetConcurrency() => _config.Settings.MaxConcurrency;
    public string GetRunDate() => _runDate;
    public string? GetTempDirPath() => _config.FilePaths.TempDirPath;
    public string? GetLogsDirectory() => _config.FilePaths.LogsDirPath;
    public RunMode RunMode { get; set; }
    public Dictionary<string, string>? GetScripts() => _config.Scripts;
    public Dictionary<string, string>? GetEnvironmentVariables() => _config.EnvironmentVariables;
    public bool GetKeepProjectFiles() => _config.Persistence.KeepProjectFiles;
    public string GetGenerationProvider() => _config.Generation.Provider;
    public Dictionary<string, object> GetGenerationParameters() => _config.Generation.Parameters;
    public string? GetAnalysisDataPath() => _config.FilePaths.AnalysisDataPath;
    public void SetAnalysisDataPath(string path) => _config.FilePaths.AnalysisDataPath = path;

    public List<ProjectModel> GetProjectModels() => _projectModels;

    public void SetRunMode(string mode) =>
        RunMode = mode switch
        {
            "collect-tests" => RunMode.CollectTests,
            "generate-tests" => RunMode.GenerateTests,
            "full-analysis" => RunMode.FullAnalysis,
            _ => RunMode
        };

    public async Task ConfigureRunAsync()
    {
        EnsureDirectory(_config.FilePaths.LogsDirPath, _runDate);
        EnsureDirectory(_config.FilePaths.TempDirPath);
        EnsureDirectory(_config.FilePaths.OutputDirPath, _runDate);
        await ReadTargetAsync();
    }

    private void EnsureDirectory(string? path, string? subfolder = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        if (!string.IsNullOrWhiteSpace(subfolder))
        {
            var full = Path.Combine(path, subfolder);
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }
    }

    private async Task ReadTargetAsync()
    {
        var target = _config.FilePaths.TargetFilePath;
        if (!File.Exists(target)) return;

        using var sr = new StreamReader(target);
        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            InitializeProjectModel(line);
        }
    }

    private void InitializeProjectModel(string projectUrl)
    {
        var (owner, repoName) = ExtractOwnerAndRepo(projectUrl);
        var dirPath = Path.Combine(_config.FilePaths.TempDirPath ?? "", repoName);
        var model = new ProjectModel(
            projectUrl, owner, repoName, _runDate,
            dirPath, _config.FilePaths.LogsDirPath,
            _config.FilePaths.OutputDirPath, _config.FilePaths.TempDirPath,
            _config.Frameworks, _config.Docker, _config.Scripts
        );
        _projectModels.Add(model);
    }

    private (string, string) ExtractOwnerAndRepo(string url)
    {
        var uri = new Uri(url);
        return (uri.Segments[1].Trim('/'), uri.Segments[2].Trim('/'));
    }
}
