using TestMap.Services.Configuration;

namespace TestMap.IntegrationTests.Configuration;

public sealed class SetupServiceIntegrationTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    /// <summary>
    /// Verifies that SetupWorkspace creates the expected directories and seed files without invoking Docker or Git checks.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public void SetupWorkspace_CreatesExpectedDirectoriesAndFiles()
    {
        // Arrange
        var parentPath = CreateTemporaryDirectory();
        var basePath = Path.Combine(parentPath, "Workspace");
        Directory.CreateDirectory(basePath);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = new SetupService(basePath, output, error);

        // Act
        service.SetupWorkspace();

        // Assert
        Assert.True(Directory.Exists(Path.Combine(basePath, "Config")));
        Assert.True(Directory.Exists(Path.Combine(basePath, "Logs")));
        Assert.True(Directory.Exists(Path.Combine(basePath, "Data")));
        Assert.True(Directory.Exists(Path.Combine(parentPath, "Temp")));
        Assert.True(File.Exists(Path.Combine(basePath, "Data", "example_project.txt")));
        Assert.True(File.Exists(Path.Combine(basePath, "Config", "default-config.json")));
        Assert.True(File.Exists(Path.Combine(basePath, ".env")));
    }

    /// <summary>
    /// Verifies that SetupWorkspace preserves an existing config file when overwrite is false.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public void SetupWorkspace_WhenOverwriteIsFalse_DoesNotReplaceExistingConfig()
    {
        // Arrange
        var parentPath = CreateTemporaryDirectory();
        var basePath = Path.Combine(parentPath, "Workspace");
        Directory.CreateDirectory(Path.Combine(basePath, "Config"));

        var configPath = Path.Combine(basePath, "Config", "default-config.json");
        File.WriteAllText(configPath, "original-config");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = new SetupService(basePath, output, error);

        // Act
        service.SetupWorkspace(overwrite: false);

        // Assert
        Assert.Equal("original-config", File.ReadAllText(configPath));
    }

    /// <summary>
    /// Verifies that SetupWorkspace replaces an existing config file when overwrite is true.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public void SetupWorkspace_WhenOverwriteIsTrue_ReplacesExistingConfig()
    {
        // Arrange
        var parentPath = CreateTemporaryDirectory();
        var basePath = Path.Combine(parentPath, "Workspace");
        Directory.CreateDirectory(Path.Combine(basePath, "Config"));

        var configPath = Path.Combine(basePath, "Config", "default-config.json");
        File.WriteAllText(configPath, "original-config");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = new SetupService(basePath, output, error);

        // Act
        service.SetupWorkspace(overwrite: true);

        // Assert
        var contents = File.ReadAllText(configPath);
        Assert.NotEqual("original-config", contents);
        Assert.Contains("\"RuntimeConfig\"", contents, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that SetupWorkspace preserves an existing example project file.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public void SetupWorkspace_WhenExampleProjectExists_DoesNotReplaceIt()
    {
        // Arrange
        var parentPath = CreateTemporaryDirectory();
        var basePath = Path.Combine(parentPath, "Workspace");
        Directory.CreateDirectory(Path.Combine(basePath, "Data"));

        var exampleProjectPath = Path.Combine(basePath, "Data", "example_project.txt");
        File.WriteAllText(exampleProjectPath, "https://github.com/example/custom-project");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = new SetupService(basePath, output, error);

        // Act
        service.SetupWorkspace();

        // Assert
        Assert.Equal("https://github.com/example/custom-project", File.ReadAllText(exampleProjectPath));
    }

    /// <summary>
    /// Verifies that SetupWorkspace preserves an existing environment file.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public void SetupWorkspace_WhenEnvFileExists_DoesNotReplaceIt()
    {
        // Arrange
        var parentPath = CreateTemporaryDirectory();
        var basePath = Path.Combine(parentPath, "Workspace");
        Directory.CreateDirectory(basePath);

        var envPath = Path.Combine(basePath, ".env");
        File.WriteAllText(envPath, "CUSTOM_API_KEY=existing-value");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = new SetupService(basePath, output, error);

        // Act
        service.SetupWorkspace();

        // Assert
        Assert.Equal("CUSTOM_API_KEY=existing-value", File.ReadAllText(envPath));
    }

    /// <summary>
    /// Verifies that SetupExternalTools validates Docker and Git and then builds the Linux image when the required context is available.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public void SetupExternalTools_WithAvailableDependencies_BuildsLinuxImage()
    {
        // Arrange
        var parentPath = CreateTemporaryDirectory();
        var basePath = Path.Combine(parentPath, "Workspace");
        Directory.CreateDirectory(basePath);
        CreateDockerLayout(basePath);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var processExecutor = new FakeSetupProcessExecutor((fileName, arguments) =>
        {
            return (fileName, arguments) switch
            {
                ("docker", "--version") => new SetupProcessExecutionResult(0, "Docker version 27.0.0", string.Empty),
                ("git", "--version") => new SetupProcessExecutionResult(0, "git version 2.49.0", string.Empty),
                _ when fileName == "docker" &&
                       arguments.StartsWith("info --format ", StringComparison.Ordinal) =>
                    new SetupProcessExecutionResult(0, "27.0.0", string.Empty),
                ("docker", "context ls") => new SetupProcessExecutionResult(
                    0,
                    "desktop-linux *\ndesktop-windows",
                    string.Empty),
                _ when fileName == "docker" &&
                       arguments.StartsWith("--context desktop-linux info --format ", StringComparison.Ordinal) =>
                    new SetupProcessExecutionResult(0, "linux", string.Empty),
                _ when fileName == "docker" && arguments.StartsWith("--context desktop-linux build ", StringComparison.Ordinal) =>
                    new SetupProcessExecutionResult(0, "linux build ok", string.Empty),
                _ => throw new InvalidOperationException($"Unexpected process invocation: {fileName} {arguments}")
            };
        });
        var service = new SetupService(basePath, output, error, processExecutor);

        // Act
        service.SetupExternalTools();

        // Assert
        Assert.Contains(processExecutor.Invocations, invocation =>
            invocation.FileName == "docker" &&
            invocation.Arguments.StartsWith("--context desktop-linux build ", StringComparison.Ordinal));
        Assert.Contains("Docker found.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Git found.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Skipping Windows image build", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that SetupExternalTools fails fast when Docker is unavailable.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public void SetupExternalTools_WhenDockerIsUnavailable_Throws()
    {
        // Arrange
        var parentPath = CreateTemporaryDirectory();
        var basePath = Path.Combine(parentPath, "Workspace");
        Directory.CreateDirectory(basePath);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var processExecutor = new FakeSetupProcessExecutor((fileName, arguments) =>
        {
            if (fileName == "docker" && arguments == "--version")
            {
                throw new InvalidOperationException("docker missing");
            }

            return new SetupProcessExecutionResult(0, string.Empty, string.Empty);
        });
        var service = new SetupService(basePath, output, error, processExecutor);

        // Act
        var action = () => service.SetupExternalTools();

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Docker is not installed or not on the PATH.", exception.Message);
    }

    public void Dispose()
    {
        foreach (var directory in Enumerable.Reverse(_directoriesToDelete))
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TestMap.IntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }

    private static void CreateDockerLayout(string basePath)
    {
        var linuxDir = Path.Combine(basePath, "Docker", "linux");
        var windowsDir = Path.Combine(basePath, "Docker", "windows");

        Directory.CreateDirectory(linuxDir);
        Directory.CreateDirectory(windowsDir);

        File.WriteAllText(Path.Combine(linuxDir, "Dockerfile"), "FROM scratch");
        File.WriteAllText(Path.Combine(windowsDir, "Dockerfile"), "FROM scratch");
    }

    private sealed class FakeSetupProcessExecutor(Func<string, string, SetupProcessExecutionResult> handler)
        : ISetupProcessExecutor
    {
        public List<Invocation> Invocations { get; } = [];

        public SetupProcessExecutionResult Run(string fileName, string arguments, bool throwOnFailure)
        {
            Invocations.Add(new Invocation(fileName, arguments, throwOnFailure));
            return handler(fileName, arguments);
        }

        public void Start(string fileName, string arguments, bool useShellExecute)
        {
            Invocations.Add(new Invocation(fileName, arguments, useShellExecute));
        }
    }

    private sealed record Invocation(string FileName, string Arguments, bool Flag);
}
