using TestMap.Models.Configuration;
using TestMap.Services.Configuration;

namespace TestMap.UnitTests.Configuration;

public sealed class ConfigurationServiceTests : IDisposable
{
    private readonly List<EnvironmentVariableScope> _scopes = [];
    private readonly List<string> _directoriesToDelete = [];

    public void Dispose()
    {
        foreach (var scope in Enumerable.Reverse(_scopes))
        {
            scope.Dispose();
        }

        foreach (var directory in Enumerable.Reverse(_directoriesToDelete))
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    /// <summary>
    /// Verifies that SetSecrets populates empty provider settings from environment variables.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void SetSecrets_UsesEnvironmentVariables_WhenConfigValuesAreEmpty()
    {
        // Arrange
        AddScope("OPENAI_API_KEY", "openai-key");
        AddScope("OPENAI_ORG_ID", "openai-org");
        AddScope("AMZ_ACCESS_KEY", "aws-access");
        AddScope("AMZ_SECRET_KEY", "aws-secret");
        AddScope("GOOGLE_GEMINI_API_KEY", "gemini-key");
        AddScope("GOOGLE_CLOUD_API_KEY", "gcloud-key");
        AddScope("GOOGLE_CLOUD_ACCESS_TOKEN", "gcloud-token");
        AddScope("GOOGLE_APPLICATION_CREDENTIALS", "credentials.json");
        AddScope("CUSTOM_API_KEY", "custom-key");

        var config = new TestMapConfig();
        var service = new ConfigurationService(config);

        // Act
        service.SetSecrets();

        // Assert
        Assert.Equal("openai-key", config.AiProviderConfig.OpenAi.ApiKey);
        Assert.Equal("openai-org", config.AiProviderConfig.OpenAi.OrgId);
        Assert.Equal("aws-access", config.AiProviderConfig.Amazon.AwsAccessKey);
        Assert.Equal("aws-secret", config.AiProviderConfig.Amazon.ApiKey);
        Assert.Equal("gemini-key", config.AiProviderConfig.GoogleGemini.ApiKey);
        Assert.Equal("gcloud-key", config.AiProviderConfig.GoogleCloud.ApiKey);
        Assert.Equal("gcloud-token", config.AiProviderConfig.GoogleCloud.AccessToken);
        Assert.Equal("credentials.json", config.AiProviderConfig.GoogleCloud.TokenPath);
        Assert.Equal("custom-key", config.AiProviderConfig.CustomOpenAi.ApiKey);
    }

    /// <summary>
    /// Verifies that SetSecrets does not overwrite provider settings that are already configured explicitly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void SetSecrets_PreservesConfiguredValues_WhenEnvironmentVariablesAlsoExist()
    {
        // Arrange
        AddScope("OPENAI_API_KEY", "env-openai-key");
        AddScope("OPENAI_ORG_ID", "env-openai-org");

        var config = new TestMapConfig();
        config.AiProviderConfig.OpenAi.ApiKey = "configured-openai-key";
        config.AiProviderConfig.OpenAi.OrgId = "configured-openai-org";
        var service = new ConfigurationService(config);

        // Act
        service.SetSecrets();

        // Assert
        Assert.Equal("configured-openai-key", config.AiProviderConfig.OpenAi.ApiKey);
        Assert.Equal("configured-openai-org", config.AiProviderConfig.OpenAi.OrgId);
    }

    /// <summary>
    /// Verifies that SetSecrets uses the first non-empty configured environment variable from the fallback list.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void SetSecrets_UsesFallbackEnvironmentVariable_WhenPrimaryValueIsMissing()
    {
        // Arrange
        AddScope("GOOGLE_GEMINI_API_KEY", null);
        AddScope("GOOGLE_API_KEY", "fallback-google-key");

        var config = new TestMapConfig();
        var service = new ConfigurationService(config);

        // Act
        service.SetSecrets();

        // Assert
        Assert.Equal("fallback-google-key", config.AiProviderConfig.GoogleGemini.ApiKey);
    }

    /// <summary>
    /// Verifies that ConfigureRunAsync creates runtime directories and initializes project models from the target file.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ConfigureRunAsync_WithTargetFile_CreatesDirectoriesAndProjectModels()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var logsPath = Path.Combine(rootPath, "Logs");
        var tempPath = Path.Combine(rootPath, "Temp");
        var outputPath = Path.Combine(rootPath, "Output");
        var dataPath = Path.Combine(rootPath, "Data");
        Directory.CreateDirectory(dataPath);

        var targetFilePath = Path.Combine(dataPath, "targets.txt");
        await File.WriteAllLinesAsync(
            targetFilePath,
            [
                "https://github.com/dotnet/runtime",
                "https://github.com/xunit/xunit"
            ]);

        var config = new TestMapConfig
        {
            RuntimeConfig =
            {
                FilePaths =
                {
                    LogsDirPath = logsPath,
                    TempDirPath = tempPath,
                    OutputDirPath = outputPath,
                    TargetFilePath = targetFilePath
                }
            }
        };
        var service = new ConfigurationService(config);

        // Act
        await service.ConfigureRunAsync();

        // Assert
        Assert.True(Directory.Exists(logsPath));
        Assert.True(Directory.Exists(Path.Combine(logsPath, service.RunDate)));
        Assert.True(Directory.Exists(tempPath));
        Assert.True(Directory.Exists(outputPath));

        Assert.Equal(2, service.ProjectModels.Count);

        var firstProject = service.ProjectModels[0];
        Assert.Equal("dotnet", firstProject.Owner);
        Assert.Equal("runtime", firstProject.RepoName);
        Assert.Equal(Path.Combine(tempPath, "runtime"), firstProject.DirectoryPath);
        Assert.Equal(Path.Combine(outputPath, "dotnet-runtime", "analysis.db"), firstProject.DatabasePath);

        var secondProject = service.ProjectModels[1];
        Assert.Equal("xunit", secondProject.Owner);
        Assert.Equal("xunit", secondProject.RepoName);
    }

    /// <summary>
    /// Verifies that ConfigureRunAsync handles a missing target file without creating project models.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ConfigureRunAsync_WithoutTargetFile_DoesNotCreateProjectModels()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var config = new TestMapConfig
        {
            RuntimeConfig =
            {
                FilePaths =
                {
                    LogsDirPath = Path.Combine(rootPath, "Logs"),
                    TempDirPath = Path.Combine(rootPath, "Temp"),
                    OutputDirPath = Path.Combine(rootPath, "Output"),
                    TargetFilePath = Path.Combine(rootPath, "Data", "missing-targets.txt")
                }
            }
        };
        var service = new ConfigurationService(config);

        // Act
        await service.ConfigureRunAsync();

        // Assert
        Assert.Empty(service.ProjectModels);
        Assert.True(Directory.Exists(config.RuntimeConfig.FilePaths.LogsDirPath));
        Assert.True(Directory.Exists(Path.Combine(config.RuntimeConfig.FilePaths.LogsDirPath!, service.RunDate)));
        Assert.True(Directory.Exists(config.RuntimeConfig.FilePaths.TempDirPath));
        Assert.True(Directory.Exists(config.RuntimeConfig.FilePaths.OutputDirPath));
    }

    private void AddScope(string name, string? value)
    {
        _scopes.Add(new EnvironmentVariableScope(name, value));
    }

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }
}
