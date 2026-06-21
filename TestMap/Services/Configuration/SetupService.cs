using System.Text.Json;
using TestMap.Models.Configuration;
using TestMap.Services.TestExecution;

namespace TestMap.Services.Configuration;

public class SetupService
{
    public const string ValidationImageName = "testmap-validation-sdk-all:latest";
    public const string AgentToolImagePrefix = "testmap-agent-eval-";
    public const string AgentBaseImageName = "testmap-dotnet-agent-base:0.1";

    private readonly string _basePath;
    private readonly string _parentDir;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly ISetupProcessExecutor _processExecutor;

    public SetupService(
        string basePath,
        TextWriter? output = null,
        TextWriter? error = null,
        ISetupProcessExecutor? processExecutor = null)
    {
        _basePath = string.IsNullOrEmpty(basePath)
            ? Directory.GetCurrentDirectory()
            : basePath;
        _parentDir = Directory.GetParent(_basePath)?.FullName ?? "";
        _output = output ?? Console.Out;
        _error = error ?? Console.Error;
        _processExecutor = processExecutor ?? new DefaultSetupProcessExecutor();
    }

    public void Setup(bool overwrite = false)
    {
        SetupWorkspace(overwrite);
        SetupExternalTools();
    }

    public void SetupExternalTools()
    {
        CheckForDocker();
        CheckForGit();
        BuildAllImages();
    }

    public void SetupWorkspace(bool overwrite = false)
    {
        CreateConfigDirectory();
        CreateLogsDirectory();
        CreateTempDirectory();
        CreateDataDirectory();
        CreateExampleProject();
        CreateConfigurationFile(overwrite);
        CreateEnvFile();
    }

    private void CheckForDocker()
    {
        if (!IsCommandAvailable("docker"))
            throw new InvalidOperationException("Docker is not installed or not on the PATH.");
        _output.WriteLine("Docker found.");
        EnsureDockerDesktopStarted();
    }

    private void CheckForGit()
    {
        if (!IsCommandAvailable("git")) throw new InvalidOperationException("Git is not installed or not on the PATH.");
        _output.WriteLine("Git found.");
    }

    private bool DockerContextExists(string contextName)
    {
        var result = _processExecutor.Run("docker", "context ls", false);
        return result.ExitCode == 0 &&
               result.StdOut.Split('\n')
                   .Any(line => line.StartsWith(contextName + " ", StringComparison.Ordinal));
    }

    private bool IsWindowsDaemon(string contextName)
    {
        return IsDockerDaemon(contextName, "windows");
    }

    private bool CanBuildWindowsImages()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        const string windowsContext = "desktop-windows";

        if (!DockerContextExists(windowsContext))
            return false;

        if (!EnsureDockerContextReady(windowsContext, "windows"))
            return false;

        return true;
    }


    private string GetDockerfile(string os)
    {
        var dockerRoot = GetDockerRoot();
        return os switch
        {
            "linux" => Path.Combine(dockerRoot, "linux", "Dockerfile"),
            "windows" => Path.Combine(dockerRoot, "windows", "Dockerfile"),
            _ => throw new InvalidOperationException($"Unknown OS: {os}")
        };
    }

    private void BuildForContext(string contextName, string dockerfilePath, string imageName, string? contextDir = null)
    {
        var dockerRoot = GetDockerRoot();
        var sourceRoot = Directory.GetParent(dockerRoot)?.FullName ?? _basePath;
        var buildContextDir = contextDir ?? dockerRoot;
        var networkArgs = CreateDockerBuildNetworkArgs(contextName);

        _output.WriteLine($"Docker source root: {sourceRoot}");
        _output.WriteLine($"Docker context dir: {buildContextDir}");
        _output.WriteLine($"Dockerfile: {dockerfilePath}");
        _output.WriteLine($"Image: {imageName}");

        var result = _processExecutor.Run(
            "docker",
            $"--context {contextName} build{networkArgs} -t {imageName} -f \"{dockerfilePath}\" \"{buildContextDir}\"",
            false);

        WriteProcessOutput(result);

        if (result.ExitCode != 0)
            throw new Exception($"Docker build failed for context '{contextName}' with exit code {result.ExitCode}");

        _output.WriteLine($"Image '{imageName}' built successfully for context '{contextName}'.");
    }

    private string CreateDockerBuildNetworkArgs(string contextName)
    {
        if (!contextName.Contains(DockerRuntimePathMapper.WindowsContextName, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var network = ReadWindowsNetworkFromConfig();
        return string.IsNullOrWhiteSpace(network)
            ? string.Empty
            : $" --network={network.Trim()}";
    }

    private string ReadWindowsNetworkFromConfig()
    {
        var configPath = Path.Combine(_basePath, "Config", "default-config.json");
        if (!File.Exists(configPath)) return string.Empty;

        try
        {
            var config = JsonSerializer.Deserialize<TestMapConfig>(
                File.ReadAllText(configPath),
                ConfigJsonSerializer.CreateOptions());

            return config?.RuntimeConfig.Docker.WindowsNetwork ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void BuildAllImages()
    {
        _output.WriteLine("=== Building Linux Validation Image ===");
        if (!EnsureDockerContextReady("desktop-linux", "linux"))
            throw new InvalidOperationException("Docker Linux context is not available.");

        var linuxDockerfile = GetDockerfile("linux");
        BuildForContext(
            "desktop-linux",
            linuxDockerfile,
            ValidationImageName
        );

        BuildAgenticToolImages();

        _output.WriteLine("=== Building Windows Validation Image ===");

        if (!CanBuildWindowsImages())
        {
            _output.WriteLine("Skipping Windows image build: Windows containers are not available on this host.");
            return;
        }

        BuildForContext(
            "desktop-windows",
            GetDockerfile("windows"),
            ValidationImageName
        );
    }

    private void BuildAgenticToolImages()
    {
        var agenticToolsRoot = GetAgenticToolsRoot();
        if (string.IsNullOrWhiteSpace(agenticToolsRoot))
        {
            _output.WriteLine("Skipping agentic tool image builds: no agentic-tools Docker directory was found.");
            return;
        }

        _output.WriteLine("=== Building Agentic Tool Base Image ===");

        var baseDockerfile = Path.Combine(agenticToolsRoot, "testmap-dotnet-agent-base", "Dockerfile");
        if (!File.Exists(baseDockerfile))
            throw new InvalidOperationException($"Agentic tool base Dockerfile not found at '{baseDockerfile}'.");

        BuildForContext(
            "desktop-linux",
            baseDockerfile,
            AgentBaseImageName,
            agenticToolsRoot);

        foreach (var toolDirectory in Directory.EnumerateDirectories(agenticToolsRoot).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var toolName = Path.GetFileName(toolDirectory);
            if (toolName.Equals("common", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("testmap-dotnet-agent-base", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var toolDockerfile = Path.Combine(toolDirectory, "Dockerfile");
            if (!File.Exists(toolDockerfile)) continue;

            EnsureAgentToolRunnerExists(toolDirectory, toolName);

            _output.WriteLine($"=== Building Agentic Tool Image: {toolName} ===");
            BuildForContext(
                "desktop-linux",
                toolDockerfile,
                $"{AgentToolImagePrefix}{toolName}:latest",
                agenticToolsRoot);
        }
    }

    private static void EnsureAgentToolRunnerExists(string toolDirectory, string toolName)
    {
        if (Directory.EnumerateFiles(toolDirectory, "run-*.sh").Any()) return;

        throw new InvalidOperationException(
            $"Agentic tool '{toolName}' must provide a runner script matching 'run-*.sh'.");
    }

    private void EnsureDockerDesktopStarted()
    {
        if (IsDockerResponsive()) return;

        if (!OperatingSystem.IsWindows()) return;

        var dockerDesktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Docker",
            "Docker",
            "Docker Desktop.exe");

        if (!File.Exists(dockerDesktopPath)) return;

        _output.WriteLine("Starting Docker Desktop...");
        _processExecutor.Start(dockerDesktopPath, string.Empty, true);

        WaitForDockerResponsive(TimeSpan.FromSeconds(3));
    }

    private bool EnsureDockerContextReady(string contextName, string expectedOs)
    {
        if (!DockerContextExists(contextName)) return false;

        if (IsDockerDaemon(contextName, expectedOs)) return true;

        if (!OperatingSystem.IsWindows()) return false;

        var dockerCliPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Docker",
            "Docker",
            "DockerCli.exe");

        if (!File.Exists(dockerCliPath)) return false;

        var switchArgument = expectedOs.Equals("windows", StringComparison.OrdinalIgnoreCase)
            ? "-SwitchWindowsEngine"
            : "-SwitchLinuxEngine";

        _output.WriteLine($"Switching Docker Desktop to {expectedOs} containers...");
        _processExecutor.Run(dockerCliPath, switchArgument, false);

        return WaitForDockerDaemon(contextName, expectedOs, TimeSpan.FromMinutes(2));
    }

    private bool IsDockerResponsive()
    {
        var result = _processExecutor.Run("docker", "info --format \"{{{{.ServerVersion}}}}\"", false);
        return result.ExitCode == 0;
    }

    private bool IsDockerDaemon(string contextName, string expectedOs)
    {
        var result = _processExecutor.Run(
            "docker",
            $"--context {contextName} info --format \"{{{{.OSType}}}}\"",
            false);

        return result.ExitCode == 0 &&
               result.StdOut.Contains(expectedOs, StringComparison.OrdinalIgnoreCase);
    }

    private void WaitForDockerResponsive(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now.Add(timeout);
        while (DateTimeOffset.Now < deadline)
        {
            if (IsDockerResponsive()) return;

            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
    }

    private bool WaitForDockerDaemon(string contextName, string expectedOs, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now.Add(timeout);
        while (DateTimeOffset.Now < deadline)
        {
            if (IsDockerDaemon(contextName, expectedOs)) return true;

            Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        return false;
    }

    private bool IsCommandAvailable(string command)
    {
        try
        {
            var result = _processExecutor.Run(command, "--version", false);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private void CreateConfigDirectory()
    {
        var path = Path.Combine(_basePath, "Config");
        Directory.CreateDirectory(path);
        _output.WriteLine($"Config directory created at: {path}");
    }

    private void CreateLogsDirectory()
    {
        var path = Path.Combine(_basePath, "Logs");
        Directory.CreateDirectory(path);
        _output.WriteLine($"Logs directory created at: {path}");
    }

    private void CreateDataDirectory()
    {
        var path = Path.Combine(_basePath, "Data");
        Directory.CreateDirectory(path);
        _output.WriteLine($"Database directory created at: {path}");
    }

    private void CreateExampleProject()
    {
        var filePath = Path.Combine(_basePath, "Data", "example_project.txt");
        if (!File.Exists(filePath))
        {
            File.Create(filePath).Close();
            File.WriteAllText(filePath, "https://github.com/dotnetcore/aspectcore-framework");
            _output.WriteLine($"Example project file created at: {filePath}");
        }
        else
        {
            _output.WriteLine($"Example project file already exists at: {filePath}");
        }
    }


    private void CreateTempDirectory()
    {
        var path = Path.Combine(_parentDir, "Temp");
        Directory.CreateDirectory(path);
        _output.WriteLine($"Temp directory created at: {path}");
    }

    private void CreateConfigurationFile(bool overwrite)
    {
        var configPath = Path.Combine(_basePath, "Config", "default-config.json");

        if (!File.Exists(configPath))
        {
            var genConfig = new GenerateConfigurationService(configPath, _basePath, _parentDir);
            genConfig.GenerateConfiguration();

            _output.WriteLine($"Configuration file created at: {configPath}");
        }
        else if (overwrite)
        {
            var genConfig = new GenerateConfigurationService(configPath, _basePath, _parentDir);
            genConfig.GenerateConfiguration();

            _output.WriteLine($"Configuration file overwritten at: {configPath}");
        }
        else
        {
            _output.WriteLine($"Configuration file already exists at: {configPath}");
        }
    }

    private void CreateEnvFile()
    {
        var envPath = Path.Combine(_basePath, ".env");

        var contents = "# Add your environment variables here\n" +
                       "### OpenAI ### \n" +
                       "OPENAI_ORG_ID=\n" +
                       "OPENAI_API_KEY=\n" +
                       "### Google Gemini ### \n" +
                       "GOOGLE_GEMINI_API_KEY=\n" +
                       "### Google Cloud / Vertex AI ### \n" +
                       "GOOGLE_CLOUD_API_KEY=\n" +
                       "GOOGLE_CLOUD_ACCESS_TOKEN=\n" +
                       "GOOGLE_APPLICATION_CREDENTIALS=\n" +
                       "### Amazon ###\n" +
                       "AMZ_ACCESS_KEY=\n" +
                       "AMZ_SECRET_KEY=\n" +
                       "### Custom ###\n" +
                       "CUSTOM_API_KEY=\n" +
                       "### GITHUB ###\n" +
                       "GITHUB_TOKEN=\n" +
                       "GITHUB_COPILOT_TOKEN=\n";

        if (!File.Exists(envPath))
        {
            File.WriteAllText(envPath, contents);
            _output.WriteLine($".env file created at: {envPath}");
        }
        else
        {
            _output.WriteLine($".env file already exists at: {envPath}");
        }
    }

    private static string ResolveDockerRoot(string basePath)
    {
        var candidates = new[]
        {
            Path.Combine(basePath, "Docker", "validation"),
            Path.Combine(basePath, "TestMap", "Docker", "validation"),
            Path.Combine(basePath, "Docker"),
            Path.Combine(basePath, "TestMap", "Docker")
        };

        foreach (var candidate in candidates)
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "linux", "Dockerfile")))
                return candidate;

        throw new InvalidOperationException(
            $"Could not locate the Docker build context from base path '{basePath}'.");
    }

    private string GetDockerRoot()
    {
        return ResolveDockerRoot(_basePath);
    }

    private string GetAgenticToolsRoot()
    {
        var candidates = new[]
        {
            Path.Combine(_basePath, "Docker", "agentic-tools"),
            Path.Combine(_basePath, "TestMap", "Docker", "agentic-tools")
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    private void WriteProcessOutput(SetupProcessExecutionResult result)
    {
        foreach (var line in SplitLines(result.StdOut))
        {
            _output.WriteLine(line);
        }

        foreach (var line in SplitLines(result.StdErr))
        {
            _error.WriteLine(line);
        }
    }

    private static IEnumerable<string> SplitLines(string output)
    {
        return output.Split(
            ["\r\n", "\n"],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
