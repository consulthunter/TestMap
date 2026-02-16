using System.Diagnostics;

namespace TestMap.Services.Configuration;

public class SetupService
{
    private readonly string _basePath;
    private readonly string _parentDir;

    public SetupService(string basePath)
    {
        _basePath = string.IsNullOrEmpty(basePath)
            ? Directory.GetCurrentDirectory()
            : basePath;
        _parentDir = Directory.GetParent(_basePath)?.FullName ?? "";
    }

    public void Setup(bool overwrite = false)
    {
        CreateConfigDirectory();
        CreateLogsDirectory();
        CreateTempDirectory();
        CreateDataDirectory();
        CreateConfigurationFile(overwrite);
        CreateEnvFile();
        CheckForDocker();
        CheckForGit();
        BuildAllImages();
    }

    private void CheckForDocker()
    {
        if (!IsCommandAvailable("docker"))
            throw new InvalidOperationException("Docker is not installed or not on the PATH.");
        Console.WriteLine("Docker found.");
    }

    private void CheckForGit()
    {
        if (!IsCommandAvailable("git")) throw new InvalidOperationException("Git is not installed or not on the PATH.");
        Console.WriteLine("Git found.");
    }
    
    private bool DockerContextExists(string contextName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = "context ls",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0 &&
               output.Split('\n')
                   .Any(line => line.StartsWith(contextName + " ", StringComparison.Ordinal));
    }
    
    private bool IsWindowsDaemon(string contextName)
    {
        string command = $"--context {contextName} info --format \"{{{{.OSType}}}}\"";
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0 &&
               output.Contains("windows", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanBuildWindowsImages()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        const string windowsContext = "desktop-windows";

        if (!DockerContextExists(windowsContext))
            return false;

        if (!IsWindowsDaemon(windowsContext))
            return false;

        return true;
    }

    
    private string GetDockerfile(string os)
    {
        var dockerRoot = Path.Combine(_basePath, "Docker");

        return os switch
        {
            "linux"   => Path.Combine(dockerRoot, "linux", "Dockerfile"),
            "windows" => Path.Combine(dockerRoot, "windows", "Dockerfile"),
            _ => throw new InvalidOperationException($"Unknown OS: {os}")
        };
    }
    private void BuildForContext(string contextName, string dockerfilePath, string imageName)
    {
        var contextDir = Path.GetDirectoryName(dockerfilePath)!;

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"--context {contextName} build -t {imageName} -f \"{dockerfilePath}\" \"{contextDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, args) => Console.WriteLine(args.Data);
        process.ErrorDataReceived  += (_, args) => Console.Error.WriteLine(args.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new Exception($"Docker build failed for context '{contextName}' with exit code {process.ExitCode}");

        Console.WriteLine($"Image '{imageName}' built successfully for context '{contextName}'.");
    }
    
    public void BuildAllImages()
    {
        Console.WriteLine("=== Building Linux Image ===");
        var linuxDockerfile = GetDockerfile("linux");
        BuildForContext(
            contextName: "desktop-linux",
            dockerfilePath: linuxDockerfile,
            imageName: "net-sdk-all:latest"
        );

        Console.WriteLine("=== Building Windows Image ===");

        if (!CanBuildWindowsImages())
        {
            Console.WriteLine("Skipping Windows image build: Windows containers are not available on this host.");
            return;
        }

        BuildForContext(
            contextName: "desktop-windows",
            dockerfilePath: GetDockerfile("windows"),
            imageName: "net-sdk-all:latest"
        );
    }

    private bool IsCommandAvailable(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            process.WaitForExit();
            return process.ExitCode == 0;
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
        Console.WriteLine($"Config directory created at: {path}");
    }

    private void CreateLogsDirectory()
    {
        var path = Path.Combine(_basePath, "Logs");
        Directory.CreateDirectory(path);
        Console.WriteLine($"Logs directory created at: {path}");
    }

    private void CreateDataDirectory()
    {
        var path = Path.Combine(_basePath, "Data");
        Directory.CreateDirectory(path);
        Console.WriteLine($"Database directory created at: {path}");
    }

    private void CreateTempDirectory()
    {
        var path = Path.Combine(_parentDir, "Temp");
        Directory.CreateDirectory(path);
        Console.WriteLine($"Temp directory created at: {path}");
    }

    private void CreateConfigurationFile(bool overwrite)
    {
        var configPath = Path.Combine(_basePath, "Config", "default-config.json");

        if (!File.Exists(configPath))
        {
            var genConfig = new GenerateConfigurationService(configPath, _basePath, _parentDir);
            genConfig.GenerateConfiguration();

            Console.WriteLine($"Configuration file created at: {configPath}");
        }
        else if (overwrite)
        {
            var genConfig = new GenerateConfigurationService(configPath, _basePath, _parentDir);
            genConfig.GenerateConfiguration();

            Console.WriteLine($"Configuration file overwritten at: {configPath}");
        }
        else
        {
            Console.WriteLine($"Configuration file already exists at: {configPath}");
        }
    }

    private void CreateEnvFile()
    {
        var envPath = Path.Combine(_basePath, ".env");

        var contents = "# Add your environment variables here\n" +
                       "### OpenAI ### \n" +
                       "OPENAI_ORD_ID=\n" +
                       "OPENAI_API_KEY=\n" +
                       "### Google ### \n" +
                       "GOOGLE_API_KEY=\n" +
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
            Console.WriteLine($".env file created at: {envPath}");
        }
        else
        {
            Console.WriteLine($".env file already exists at: {envPath}");
        }
    }
}