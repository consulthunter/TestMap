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
    public string RunDate { get; } = DateTime.UtcNow.ToString(config.RuntimeConfig.RunDateFormat);
    public List<ProjectModel> ProjectModels { get; } = new();

    public async Task ConfigureRunAsync()
    {
        EnsureDirectory(Config.RuntimeConfig.FilePaths.LogsDirPath, RunDate);
        EnsureDirectory(Config.RuntimeConfig.FilePaths.TempDirPath);
        EnsureDirectory(Config.RuntimeConfig.FilePaths.OutputDirPath);
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
        Config.AiProviderConfig.OpenAi.OrgId = GetConfiguredValue(
            Config.AiProviderConfig.OpenAi.OrgId,
            "OPENAI_ORG_ID");
        Config.AiProviderConfig.OpenAi.ApiKey = GetConfiguredValue(
            Config.AiProviderConfig.OpenAi.ApiKey,
            "OPENAI_API_KEY");

        Config.AiProviderConfig.Amazon.AwsAccessKey = GetConfiguredValue(
            Config.AiProviderConfig.Amazon.AwsAccessKey,
            "AMZ_ACCESS_KEY");
        Config.AiProviderConfig.Amazon.ApiKey = GetConfiguredValue(
            Config.AiProviderConfig.Amazon.ApiKey,
            "AMZ_SECRET_KEY");

        Config.AiProviderConfig.GoogleGemini.ApiKey = GetConfiguredValue(
            Config.AiProviderConfig.GoogleGemini.ApiKey,
            "GOOGLE_GEMINI_API_KEY",
            "GOOGLE_API_KEY");

        Config.AiProviderConfig.GoogleCloud.ApiKey = GetConfiguredValue(
            Config.AiProviderConfig.GoogleCloud.ApiKey,
            "GOOGLE_CLOUD_API_KEY");
        Config.AiProviderConfig.GoogleCloud.AccessToken = GetConfiguredValue(
            Config.AiProviderConfig.GoogleCloud.AccessToken,
            "GOOGLE_CLOUD_ACCESS_TOKEN");
        Config.AiProviderConfig.GoogleCloud.TokenPath = GetConfiguredValue(
            Config.AiProviderConfig.GoogleCloud.TokenPath,
            "GOOGLE_APPLICATION_CREDENTIALS");

        Config.AiProviderConfig.CustomOpenAi.ApiKey = GetConfiguredValue(
            Config.AiProviderConfig.CustomOpenAi.ApiKey,
            "CUSTOM_API_KEY");
    }

    private async Task ReadTargetAsync()
    {
        var target = Config.RuntimeConfig.FilePaths.TargetFilePath;
        if (!File.Exists(target)) return;

        using var sr = new StreamReader(target);
        string? line;
        while ((line = await sr.ReadLineAsync()) != null) InitializeProjectModel(line);
    }

    private void InitializeProjectModel(string repoUrl)
    {
        var (owner, repoName) = Utilities.Utilities.ExtractOwnerAndRepo(repoUrl);

        var dirPath = Path.Combine(Config.RuntimeConfig.FilePaths.TempDirPath ?? "", repoName);

        var repoOutputPath = Path.Combine(Config.RuntimeConfig.FilePaths.OutputDirPath ?? "", $"{owner}-{repoName}");

        var dbFilePath = Path.Combine(repoOutputPath, "analysis.db");

        var model = new ProjectModel(
            repoUrl, owner, repoName, RunDate,
            dirPath,
            Config.RuntimeConfig.FilePaths.LogsDirPath,
            Config.RuntimeConfig.FilePaths.OutputDirPath,
            Config.RuntimeConfig.FilePaths.TempDirPath,
            dbFilePath,
            Config);

        ProjectModels.Add(model);
    }

    private static string GetConfiguredValue(string currentValue, params string[] environmentVariables)
    {
        if (!string.IsNullOrWhiteSpace(currentValue))
        {
            return currentValue;
        }

        foreach (var variable in environmentVariables)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return currentValue;
    }
}
