
using System.Diagnostics;

namespace TestMap.Services.Configuration
{
    public class SetupService
    {
        private readonly string _basePath;
        private readonly string _parentDir;

        public SetupService(string basePath = null)
        {
            _basePath = basePath ?? Directory.GetCurrentDirectory();
            _parentDir = Directory.GetParent(_basePath).FullName;
        }

        public void Setup()
        {
            CreateConfigDirectory();
            CreateLogsDirectory();
            CreateTempDirectory();
            CreateDataDirectory();
            CreateConfigurationFile();
            CreateEnvFile();
            CheckForDocker();
            CheckForGit();
            BuildDockerImage();
        }

    private void CheckForDocker()
    {
        if (!IsCommandAvailable("docker"))
        {
            throw new InvalidOperationException("Docker is not installed or not on the PATH.");
        }
        Console.WriteLine("Docker found.");
    }

    private void CheckForGit()
    {
        if (!IsCommandAvailable("git"))
        {
            throw new InvalidOperationException("Git is not installed or not on the PATH.");
        }
        Console.WriteLine("Git found.");
    }

    private void BuildDockerImage(string imageName = "net-sdk-all:latest")
    {
        string dockerfilePath = Path.Combine(_basePath, "Docker", "Dockerfile");
        if (!File.Exists(dockerfilePath))
        {
            throw new FileNotFoundException($"Dockerfile not found at {dockerfilePath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"build -t {imageName} -f \"{dockerfilePath}\" \"{_basePath}/Docker\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
        process.ErrorDataReceived += (sender, args) => Console.Error.WriteLine(args.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new Exception($"Docker build failed with exit code {process.ExitCode}");

        Console.WriteLine($"Docker image '{imageName}' built successfully.");
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
        
        private void CreateConfigurationFile()
        {
            var configPath = Path.Combine(_basePath, "Config", "default-config.json");

            if (!File.Exists(configPath))
            {
                var genConfig = new GenerateConfigurationService(configPath, _basePath, _parentDir);
                genConfig.GenerateConfiguration();

                Console.WriteLine($"Configuration file created at: {configPath}");
            }
            else
            {
                Console.WriteLine($"Configuration file already exists at: {configPath}");
            }
        }

        private void CreateEnvFile()
        {
            var envPath = Path.Combine(_basePath, ".env");

            if (!File.Exists(envPath))
            {
                File.WriteAllText(envPath, "# Add your environment variables here\n");
                Console.WriteLine($".env file created at: {envPath}");
            }
            else
            {
                Console.WriteLine($".env file already exists at: {envPath}");
            }
        }
    }
}
