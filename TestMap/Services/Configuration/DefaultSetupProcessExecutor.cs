using System.Diagnostics;

namespace TestMap.Services.Configuration;

public sealed class DefaultSetupProcessExecutor : ISetupProcessExecutor
{
    public SetupProcessExecutionResult Run(string fileName, string arguments, bool throwOnFailure)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (throwOnFailure && process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Command failed: {fileName} {arguments}{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");

        return new SetupProcessExecutionResult(process.ExitCode, stdout, stderr);
    }

    public void Start(string fileName, string arguments, bool useShellExecute)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = useShellExecute
        });
    }
}
