using TestMap;
using TestMap.Services.Configuration;

namespace TestMap.EndToEndTests;

public sealed class SetupCommandEndToEndTests : IDisposable
{
    private static readonly SemaphoreSlim ProgramMutationSemaphore = new(1, 1);
    private readonly List<string> _directoriesToDelete = [];

    /// <summary>
    /// Verifies that the setup command creates the expected workspace artifacts when Docker and Git are available on PATH.
    /// </summary>
    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("Execution", "LocalOnly")]
    public async Task SetupCommand_WithWorkspacePath_CreatesExpectedArtifacts()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var workspacePath = Path.Combine(rootPath, "Workspace");
        Directory.CreateDirectory(workspacePath);
        CreateDockerLayout(workspacePath);
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
                _ when fileName == "docker" &&
                       arguments.StartsWith("--context desktop-windows info --format ", StringComparison.Ordinal) =>
                    new SetupProcessExecutionResult(0, "windows", string.Empty),
                _ when fileName == "docker" &&
                       arguments.StartsWith("--context desktop-linux build ", StringComparison.Ordinal) =>
                    new SetupProcessExecutionResult(0, "linux build ok", string.Empty),
                _ when fileName == "docker" &&
                       arguments.StartsWith("--context desktop-windows build ", StringComparison.Ordinal) =>
                    new SetupProcessExecutionResult(0, "windows build ok", string.Empty),
                _ => throw new InvalidOperationException($"Unexpected process invocation: {fileName} {arguments}")
            };
        });
        var originalFactory = Program.SetupServiceFactory;

        // Act
        int exitCode;
        await ProgramMutationSemaphore.WaitAsync();
        try
        {
            Program.SetupServiceFactory = options => new SetupService(options.BasePath, processExecutor: processExecutor);
            exitCode = await Program.Main(["setup", "--base-path", workspacePath]);
        }
        finally
        {
            Program.SetupServiceFactory = originalFactory;
            ProgramMutationSemaphore.Release();
        }

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(Path.Combine(workspacePath, "Config")));
        Assert.True(Directory.Exists(Path.Combine(workspacePath, "Logs")));
        Assert.True(Directory.Exists(Path.Combine(workspacePath, "Data")));
        Assert.True(Directory.Exists(Path.Combine(rootPath, "Temp")));
        Assert.True(File.Exists(Path.Combine(workspacePath, "Config", "default-config.json")));
        Assert.True(File.Exists(Path.Combine(workspacePath, ".env")));
        Assert.True(File.Exists(Path.Combine(workspacePath, "Data", "example_project.txt")));
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
        var path = Path.Combine(Path.GetTempPath(), "TestMap.EndToEndTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }

    private static void CreateDockerLayout(string workspacePath)
    {
        var linuxDir = Path.Combine(workspacePath, "Docker", "linux");
        var windowsDir = Path.Combine(workspacePath, "Docker", "windows");
        Directory.CreateDirectory(linuxDir);
        Directory.CreateDirectory(windowsDir);
        File.WriteAllText(Path.Combine(linuxDir, "Dockerfile"), "FROM scratch");
        File.WriteAllText(Path.Combine(windowsDir, "Dockerfile"), "FROM scratch");
    }

    private sealed class FakeSetupProcessExecutor(Func<string, string, SetupProcessExecutionResult> handler)
        : ISetupProcessExecutor
    {
        public SetupProcessExecutionResult Run(string fileName, string arguments, bool throwOnFailure)
        {
            return handler(fileName, arguments);
        }

        public void Start(string fileName, string arguments, bool useShellExecute)
        {
        }
    }
}
