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
using TestMap.Models.Targets;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;
using TestMap.Services.Logging;
using System.Globalization;

namespace TestMap.Services.Configuration;

/// <summary>
///     ConfigurationService
///     Takes in the configuration parsed from the JSON
///     Configures variables for the run.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ProjectLogDirectoryAllocator _logDirectoryAllocator;

    public ConfigurationService(
        TestMapConfig config,
        ProjectLogDirectoryAllocator? logDirectoryAllocator = null,
        DateTimeOffset? runStartedAtUtc = null)
    {
        Config = config;
        _logDirectoryAllocator = logDirectoryAllocator ?? new ProjectLogDirectoryAllocator();
        RunStartedAtUtc = runStartedAtUtc ?? DateTimeOffset.UtcNow;
        if (RunStartedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("The configured run timestamp must be UTC.", nameof(runStartedAtUtc));
        RunDate = RunStartedAtUtc.ToString(config.RuntimeConfig.RunDateFormat);
    }

    public TestMapConfig Config { get; }
    public RunMode RunMode { get; set; }
    public DateTimeOffset RunStartedAtUtc { get; }
    public string RunDate { get; }
    public List<ProjectModel> ProjectModels { get; } = new();
    public TargetManifest? TargetManifest { get; private set; }
    public string? TargetManifestSha256 { get; private set; }
    public string? TargetExecutionReportPath { get; private set; }

    public async Task ConfigureRunAsync()
    {
        EnsureDirectory(
            Config.RuntimeConfig.FilePaths.LogsDirPath,
            RunStartedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
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
            "GEMINI_API_KEY",
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

        Config.AiProviderConfig.Anthropic.ApiKey = GetConfiguredValue(
            Config.AiProviderConfig.Anthropic.ApiKey,
            "ANTHROPIC_API_KEY",
            "ANTHROPIC_KEY");
    }

    private async Task ReadTargetAsync()
    {
        var target = Config.RuntimeConfig.FilePaths.TargetFilePath;
        if (string.IsNullOrWhiteSpace(target)) return;
        if (!File.Exists(target))
            throw new FileNotFoundException(
                $"Configured target source does not exist: {Path.GetFullPath(target)}",
                target);

        var fingerprint = new TargetFingerprintService();
        var reader = new TargetSourceReader(new TargetManifestSerializer(fingerprint), fingerprint);
        var source = await reader.ReadAsync(
            target,
            RunMode == RunMode.Experiment ? TargetSourceMode.MeasuredExperiment : TargetSourceMode.Discovery);
        TargetManifest = source.Manifest;
        TargetManifestSha256 = source.ManifestSha256;
        if (source.Manifest is not null)
        {
            TargetExecutionReportPath = Path.Combine(
                Config.RuntimeConfig.FilePaths.OutputDirPath ?? Directory.GetCurrentDirectory(),
                $"target-execution-{source.ManifestSha256![..12]}.csv");
            foreach (var repositoryTarget in source.Targets)
                InitializeProjectModel(repositoryTarget, source.Manifest, source.ManifestSha256!);
            return;
        }

        foreach (var repoUrl in source.LegacyUrls)
            InitializeProjectModel(repoUrl);
    }

    private void InitializeProjectModel(RepositoryTarget target, TargetManifest manifest, string manifestSha256)
    {
        var parts = target.Repository.Split('/');
        var paths = new TargetPathResolver().Resolve(
            target,
            Config.RuntimeConfig.FilePaths.TempDirPath ?? string.Empty,
            Config.RuntimeConfig.FilePaths.OutputDirPath ?? string.Empty,
            Config.RuntimeConfig.FilePaths.LogsDirPath ?? string.Empty,
            RunStartedAtUtc);
        var model = new ProjectModel(
            target.Url, parts[0], parts[1], RunDate,
            paths.WorkspacePath,
            Config.RuntimeConfig.FilePaths.LogsDirPath,
            Config.RuntimeConfig.FilePaths.OutputDirPath,
            Config.RuntimeConfig.FilePaths.TempDirPath,
            paths.DatabasePath,
            Config,
            RunStartedAtUtc,
            _logDirectoryAllocator);
        model.BindTarget(target);
        model.OutputPath = paths.ArtifactPath;
        model.MaterializedRevision = new MaterializedRevision(
            target.TargetId, target.Repository, target.Commit, null, null, paths,
            manifestSha256, manifest.Source.Sha256, null, MaterializationStatus.Pending);
        ProjectModels.Add(model);
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
            Config,
            RunStartedAtUtc,
            _logDirectoryAllocator);

        ProjectModels.Add(model);
    }

    private static string GetConfiguredValue(string currentValue, params string[] environmentVariables)
    {
        if (!string.IsNullOrWhiteSpace(currentValue)) return currentValue;

        foreach (var variable in environmentVariables)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return currentValue;
    }
}
