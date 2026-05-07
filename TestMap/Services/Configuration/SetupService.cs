namespace TestMap.Services.Configuration;

public class SetupService
{
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

    private void BuildForContext(string contextName, string dockerfilePath, string imageName)
    {
        var dockerRoot = GetDockerRoot();
        var sourceRoot = Directory.GetParent(dockerRoot)?.FullName ?? _basePath;
        var contextDir = dockerRoot;

        _output.WriteLine($"Docker source root: {sourceRoot}");
        _output.WriteLine($"Docker context dir: {contextDir}");
        _output.WriteLine($"Dockerfile: {dockerfilePath}");
        _output.WriteLine($"Image: {imageName}");

        var result = _processExecutor.Run(
            "docker",
            $"--context {contextName} build -t {imageName} -f \"{dockerfilePath}\" \"{contextDir}\"",
            false);

        WriteProcessOutput(result);

        if (result.ExitCode != 0)
            throw new Exception($"Docker build failed for context '{contextName}' with exit code {result.ExitCode}");

        _output.WriteLine($"Image '{imageName}' built successfully for context '{contextName}'.");
    }

    public void BuildAllImages()
    {
        _output.WriteLine("=== Building Linux Image ===");
        if (!EnsureDockerContextReady("desktop-linux", "linux"))
            throw new InvalidOperationException("Docker Linux context is not available.");

        var linuxDockerfile = GetDockerfile("linux");
        BuildForContext(
            "desktop-linux",
            linuxDockerfile,
            "net-sdk-all:latest"
        );

        _output.WriteLine("=== Building Windows Image ===");

        if (!CanBuildWindowsImages())
        {
            _output.WriteLine("Skipping Windows image build: Windows containers are not available on this host.");
            return;
        }

        BuildForContext(
            "desktop-windows",
            GetDockerfile("windows"),
            "net-sdk-all:latest"
        );
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
                       "GITHUB_TOKEN=\n";

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
