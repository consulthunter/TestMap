using System.Diagnostics;
using TestMap.App;

namespace TestMap.Services.TestExecution;

public sealed class DockerCommandRunner
{
    private readonly ProjectContext _context;
    private readonly DockerRuntimePathMapper _pathMapper;

    public DockerCommandRunner(ProjectContext context, DockerRuntimePathMapper pathMapper)
    {
        _context = context;
        _pathMapper = pathMapper;
    }

    public async Task RunProcessAsync(string fileName, string arguments, string? workingDir = null)
    {
        var result = await RunProcessAllowFailureAsync(fileName, arguments, workingDir);
        if (result.ExitCode != 0)
            throw new ProcessExecutionException(fileName, arguments, result.StdOut, result.StdErr, result.ExitCode);
    }

    public async Task<ProcessExecutionResult> RunProcessAllowFailureAsync(
        string fileName,
        string arguments,
        string? workingDir = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var output = await stdoutTask;
        var error = await stderrTask;

        return new ProcessExecutionResult(process.ExitCode, output, error);
    }

    public async Task EnsureDockerContextReadyAsync(string context)
    {
        var expectedOs = _pathMapper.ResolveExpectedOs(context);

        if (!await DockerContextExistsAsync(context))
            throw new InvalidOperationException($"Docker context '{context}' was not found.");

        await EnsureDockerDesktopStartedAsync();

        if (await IsDockerDaemonAsync(context, expectedOs)) return;

        if (OperatingSystem.IsWindows() &&
            (_pathMapper.IsWindowsContext(context) ||
             context.Contains(DockerRuntimePathMapper.LinuxContextName, StringComparison.OrdinalIgnoreCase)))
            await SwitchDockerDesktopEngineAsync(expectedOs);

        if (!await WaitForDockerDaemonAsync(context, expectedOs, TimeSpan.FromMinutes(2)))
            throw new InvalidOperationException(
                $"Docker context '{context}' is not ready with a {expectedOs} daemon.");
    }

    public async Task<bool> DockerContextExistsAsync(string context)
    {
        var result = await RunProcessAllowFailureAsync("docker", $"context inspect {QuoteDockerArgument(context)}");
        return result.ExitCode == 0;
    }

    public async Task<bool> IsDockerResponsiveAsync()
    {
        var result = await RunProcessAllowFailureAsync("docker", "info --format \"{{{{.ServerVersion}}}}\"");
        return result.ExitCode == 0;
    }

    public async Task<bool> IsDockerDaemonAsync(string context, string expectedOs)
    {
        var result = await RunProcessAllowFailureAsync(
            "docker",
            $"--context {context} info --format \"{{{{.OSType}}}}\"");

        return result.ExitCode == 0 &&
               result.StdOut.Contains(expectedOs, StringComparison.OrdinalIgnoreCase);
    }

    public static string QuoteDockerArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private async Task EnsureDockerDesktopStartedAsync()
    {
        if (await IsDockerResponsiveAsync()) return;

        if (!OperatingSystem.IsWindows()) return;

        var dockerDesktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Docker",
            "Docker",
            "Docker Desktop.exe");

        if (!File.Exists(dockerDesktopPath)) return;

        _context.Project.Logger?.Information("Starting Docker Desktop.");
        Process.Start(new ProcessStartInfo
        {
            FileName = dockerDesktopPath,
            UseShellExecute = true
        });

        await WaitForDockerResponsiveAsync(TimeSpan.FromSeconds(5));
    }

    private async Task SwitchDockerDesktopEngineAsync(string expectedOs)
    {
        var dockerCliPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Docker",
            "Docker",
            "DockerCli.exe");

        if (!File.Exists(dockerCliPath)) return;

        var switchArgument = expectedOs.Equals("windows", StringComparison.OrdinalIgnoreCase)
            ? "-SwitchWindowsEngine"
            : "-SwitchLinuxEngine";

        _context.Project.Logger?.Information("Switching Docker Desktop to {DockerEngine} containers.", expectedOs);
        await RunProcessAllowFailureAsync(dockerCliPath, switchArgument);
    }

    private async Task WaitForDockerResponsiveAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now.Add(timeout);
        while (DateTimeOffset.Now < deadline)
        {
            if (await IsDockerResponsiveAsync()) return;

            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    private async Task<bool> WaitForDockerDaemonAsync(string context, string expectedOs, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now.Add(timeout);
        while (DateTimeOffset.Now < deadline)
        {
            if (await IsDockerDaemonAsync(context, expectedOs)) return true;

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        return false;
    }
}

public sealed record ProcessExecutionResult(int ExitCode, string StdOut, string StdErr);

public sealed class ProcessExecutionException : Exception
{
    public ProcessExecutionException(string fileName, string arguments, string stdOut, string stdErr, int exitCode)
        : base($"Command failed: {fileName} {arguments}")
    {
        FileName = fileName;
        Arguments = arguments;
        StdOut = stdOut;
        StdErr = stdErr;
        ExitCode = exitCode;
    }

    public string FileName { get; }
    public string Arguments { get; }
    public string StdOut { get; }
    public string StdErr { get; }
    public int ExitCode { get; }

    public string ToDiagnosticText()
    {
        return
            $"Command: {FileName} {Arguments}{Environment.NewLine}ExitCode: {ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{StdOut}{Environment.NewLine}STDERR:{Environment.NewLine}{StdErr}";
    }
}