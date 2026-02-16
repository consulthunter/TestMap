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
using TestMap.Utilities;

namespace TestMap.Services.Configuration;

/// <summary>
///     ConfigurationService
///     Takes in the configuration parsed from the JSON
///     Configures variables for the run.
/// </summary>
public class ConfigurationService(TestMapConfig config) : IConfigurationService
{
    public TestMapConfig Config { get; } = config;
    public RunMode RunMode { get; set; }
    public string RunDate { get; } = DateTime.UtcNow.ToString(config.Settings.RunDateFormat);
    public List<ProjectModel> ProjectModels { get; } = new();

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

    public void SetSecrets()
    {
        // OpenAI
        Config.OpenAi.OrgId = Environment.GetEnvironmentVariable("OPENAI_ORD_ID") ?? "";
        Config.OpenAi.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        // Amazon
        Config.Amazon.AwsAccessKey = Environment.GetEnvironmentVariable("AMZ_ACCESS_KEY") ?? "";
        Config.Amazon.AwsSecretKey = Environment.GetEnvironmentVariable("AMZ_SECRET_KEY") ?? "";
        // Google
        Config.Google.ApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
        // Custom
        Config.Custom.ApiKey = Environment.GetEnvironmentVariable("CUSTOM_API_KEY") ?? "";
    }

    private async Task ReadTargetAsync()
    {
        var target = Config.FilePaths.TargetFilePath;
        if (!File.Exists(target)) return;

        using var sr = new StreamReader(target);
        string? line;
        while ((line = await sr.ReadLineAsync()) != null) InitializeProjectModel(line);
    }

    private void InitializeProjectModel(string repoUrl)
    {
        var (owner, repoName) = Utilities.Utilities.ExtractOwnerAndRepo(repoUrl);

        var dirPath = Path.Combine(Config.FilePaths.TempDirPath ?? "", repoName);

        var repoOutputPath = Path.Combine(Config.FilePaths.OutputDirPath ?? "", repoName);

        var dbFilePath = Path.Combine(repoOutputPath, "analysis.db");

        var model = new ProjectModel(
            repoUrl, owner, repoName, RunDate,
            dirPath,
            config.FilePaths.LogsDirPath,
            config.FilePaths.OutputDirPath,
            Config.FilePaths.TempDirPath,
            Config.Frameworks,
            dbFilePath,
            Config);

        ProjectModels.Add(model);
    }
}