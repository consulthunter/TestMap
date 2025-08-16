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
public class ConfigurationService(TestMapConfig config) : IConfigurationService
{
    public TestMapConfig Config { get; } = config;
    public RunMode RunMode { get; set;  }
    public string RunDate { get; } = DateTime.UtcNow.ToString(config.Settings.RunDateFormat);
    public List<ProjectModel> ProjectModels { get; } = new();

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
        EnsureDirectory(Config.FilePaths.LogsDirPath, RunDate);
        EnsureDirectory(Config.FilePaths.TempDirPath);
        EnsureDirectory(Config.FilePaths.OutputDirPath);
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
        var target = Config.FilePaths.TargetFilePath;
        if (!File.Exists(target)) return;

        using var sr = new StreamReader(target);
        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            InitializeProjectModel(line);
        }
    }

    private void InitializeProjectModel(string repoUrl)
    {
        var (owner, repoName) = ExtractOwnerAndRepo(repoUrl);
        
        var dirPath = Path.Combine(Config.FilePaths.TempDirPath ?? "", repoName);
        
        var repoOutputPath = Path.Combine(Config.FilePaths.OutputDirPath ?? "", repoName);

        var dbFilePath = Path.Combine(repoOutputPath, "analysis.db");

        var model = new ProjectModel(
            repoUrl, owner, repoName, RunDate,
            directoryPath: dirPath,
            logsDirPath: config.FilePaths.LogsDirPath,
            outputDirPath: config.FilePaths.OutputDirPath,
            tempDirPath: Config.FilePaths.TempDirPath,
            testingFrameworks: Config.Frameworks,
            docker: Config.Docker,
            databasePath: dbFilePath,
            config: Config);

        ProjectModels.Add(model);
    }


    private (string, string) ExtractOwnerAndRepo(string url)
    {
        var uri = new Uri(url);
        return (uri.Segments[1].Trim('/'), uri.Segments[2].Trim('/'));
    }
}
