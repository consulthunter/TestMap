using TestMap.Services.Configuration;

namespace TestMap.UnitTests.Configuration;

public sealed class SetupServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildAllImages_WithValidationAndAgenticToolDockerfiles_BuildsExpectedImageTags()
    {
        var basePath = CreateTemporaryDirectory();
        CreateDockerfile(basePath, "Docker", "validation", "linux", "Dockerfile");
        CreateDockerfile(basePath, "Docker", "validation", "windows", "Dockerfile");
        CreateDockerfile(basePath, "Docker", "agentic-tools", "testmap-dotnet-agent-base", "Dockerfile");
        CreateDockerfile(basePath, "Docker", "agentic-tools", "codex", "Dockerfile");
        CreateFile(basePath, "Docker", "agentic-tools", "codex", "run-codex.sh");
        CreateDockerfile(basePath, "Docker", "agentic-tools", "openhands", "Dockerfile");
        CreateFile(basePath, "Docker", "agentic-tools", "openhands", "run-openhands.sh");
        CreateDockerfile(basePath, "Docker", "agentic-tools", "common", "Dockerfile");

        var executor = new FakeSetupProcessExecutor();
        var output = new StringWriter();
        var service = new SetupService(basePath, output, new StringWriter(), executor);

        service.BuildAllImages();

        Assert.Contains(executor.Commands, x =>
            x.Arguments.Contains("-t testmap-validation-sdk-all:latest", StringComparison.Ordinal) &&
            x.Arguments.Contains(Path.Combine("Docker", "validation"), StringComparison.Ordinal));
        Assert.Contains(executor.Commands, x =>
            x.Arguments.Contains("-t testmap-dotnet-agent-base:0.1", StringComparison.Ordinal) &&
            x.Arguments.Contains(Path.Combine("Docker", "agentic-tools"), StringComparison.Ordinal));
        Assert.Contains(executor.Commands, x =>
            x.Arguments.Contains("-t testmap-agent-eval-codex:latest", StringComparison.Ordinal));
        Assert.Contains(executor.Commands, x =>
            x.Arguments.Contains("-t testmap-agent-eval-openhands:latest", StringComparison.Ordinal));
        Assert.DoesNotContain(executor.Commands, x =>
            x.Arguments.Contains("-t testmap-agent-eval-common:latest", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildAllImages_WithAgentToolMissingRunner_Throws()
    {
        var basePath = CreateTemporaryDirectory();
        CreateDockerfile(basePath, "Docker", "validation", "linux", "Dockerfile");
        CreateDockerfile(basePath, "Docker", "agentic-tools", "testmap-dotnet-agent-base", "Dockerfile");
        CreateDockerfile(basePath, "Docker", "agentic-tools", "codex", "Dockerfile");

        var service = new SetupService(basePath, new StringWriter(), new StringWriter(), new FakeSetupProcessExecutor());

        var exception = Assert.Throws<InvalidOperationException>(() => service.BuildAllImages());

        Assert.Contains("must provide a runner script", exception.Message, StringComparison.Ordinal);
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
        var path = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }

    private static void CreateDockerfile(string basePath, params string[] pathParts)
    {
        CreateFile(basePath, pathParts);
    }

    private static void CreateFile(string basePath, params string[] pathParts)
    {
        var path = Path.Combine(new[] { basePath }.Concat(pathParts).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "FROM scratch");
    }

    private sealed class FakeSetupProcessExecutor : ISetupProcessExecutor
    {
        public List<CommandCall> Commands { get; } = [];

        public SetupProcessExecutionResult Run(string fileName, string arguments, bool throwOnFailure)
        {
            Commands.Add(new CommandCall(fileName, arguments));

            if (fileName == "docker" && arguments == "context ls")
            {
                return new SetupProcessExecutionResult(
                    0,
                    "desktop-linux *\ndesktop-windows\n",
                    string.Empty);
            }

            if (fileName == "docker" &&
                arguments.Contains("--context desktop-linux info", StringComparison.Ordinal))
            {
                return new SetupProcessExecutionResult(0, "linux", string.Empty);
            }

            if (fileName == "docker" &&
                arguments.Contains("--context desktop-windows info", StringComparison.Ordinal))
            {
                return new SetupProcessExecutionResult(0, "windows", string.Empty);
            }

            return new SetupProcessExecutionResult(0, string.Empty, string.Empty);
        }

        public void Start(string fileName, string arguments, bool useShellExecute)
        {
            Commands.Add(new CommandCall(fileName, arguments));
        }
    }

    private sealed record CommandCall(string FileName, string Arguments);
}
