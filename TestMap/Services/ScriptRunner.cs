/*
 * consulthunter
 * 2024-11-07
 * This is a method for running custom
 * batch or shell scripts
 *
 * This was mostly so that TestMap could build
 * and run the tests of the targeted repos
 *
 * However, there were several issues with this approach
 * so this is mostly deprecated
 *
 * Only the DeleteProjectService.cs, uses the script runner
 * to delete the repo from the temp directory
 *
 * This is left here for future uses.
 * ScriptRunner.cs
 */

using System.Diagnostics;

namespace TestMap.Services;

public class ScriptRunner
{
    /// <summary>
    ///     Default constructor
    /// </summary>
    public ScriptRunner()
    {
        Errors = new List<string>();
        Output = new List<string>();
        EnvironmentVariables = new Dictionary<string, string>();
    }

    /// <summary>
    ///     Constructor with environment variables available
    /// </summary>
    /// <param name="environmentVariables"></param>
    public ScriptRunner(Dictionary<string, string> environmentVariables)
    {
        Errors = new List<string>();
        Output = new List<string>();
        EnvironmentVariables = environmentVariables;
    }

    public List<string> Errors { get; }
    public List<string> Output { get; }
    private Dictionary<string, string> EnvironmentVariables { get; }
    public bool HasError { get; private set; }

    /// <summary>
    ///     Default method for running custom scripts
    /// </summary>
    /// <param name="arguments">Arguments to be passed to the batch file</param>
    /// <param name="batchFilePath">Absolute filepath for the batch file</param>
    public async Task RunScriptAsync(List<string> arguments, string batchFilePath)
    {
        // Create the process start info
        var startInfo = new ProcessStartInfo
        {
            FileName = batchFilePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Arguments = string.Join(" ", arguments)
        };

        try
        {
            using var process = new Process();
            process.StartInfo = startInfo;
            process.Start();

            // Read the output and error streams
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            // Wait for the process to exit
            await process.WaitForExitAsync();
            // Print output and error
            Output.AddRange(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
            Errors.AddRange(error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

            // Check exit code
            if (process.ExitCode != 0)
            {
                HasError = true;
                Errors.Add($"Batch file execution failed with exit code: {process.ExitCode}");
            }
        }
        catch (Exception e)
        {
            HasError = true;
            Errors.Add(e.Message);
        }
    }

    /// <summary>
    ///     Custom scripts may need access to certain environment variables
    ///     or override other environment variables
    /// </summary>
    /// <param name="arguments">Arguments to be passed to the batch file</param>
    /// <param name="batchFilePath">Absolute filepath for the batch file</param>
    public async Task RunScriptWithEnvironmentVariablesAsync(List<string> arguments, string batchFilePath)
    {
        var msbuildPath = EnvironmentVariables.TryGetValue("MSBUILD_EXE_PATH", out var path) ? path : "";
        arguments.Add(msbuildPath);

        var argsString = string.Join(' ', arguments.Select(args => $"\"{args}\""));
        // Create the process start info
        var startInfo = new ProcessStartInfo
        {
            FileName = batchFilePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Arguments = $@"{argsString}"
        };

        foreach (var environmentVariable in EnvironmentVariables)
            try
            {
                startInfo.EnvironmentVariables[environmentVariable.Key] = environmentVariable.Value;
            }
            catch (KeyNotFoundException ex)
            {
                HasError = true;
                Errors.Add(ex.Message);
            }


        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        // Read the output and error streams
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        // Wait for the process to exit
        await process.WaitForExitAsync();
        // Print output and error
        Output.AddRange(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Errors.AddRange(error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        // Check exit code
        if (process.ExitCode != 0)
        {
            HasError = true;
            Errors.Add($"Batch file execution failed with exit code: {process.ExitCode}");
        }
    }

    /// <summary>
    ///     Custom scripts may hang, the timeout is used to kill and restart the script
    /// </summary>
    /// <param name="arguments">Arguments to be passed to the batch file</param>
    /// <param name="batchFilePath">Absolute filepath for the batch file</param>
    /// <param name="timeoutMinutes">Timeout in minutes for the batch file</param>
    /// <param name="maxRetries">Maximum number of retries for the batch file</param>
    public async Task RunScriptWithTimeoutsAsync(List<string> arguments, string batchFilePath, int timeoutMinutes,
        int maxRetries)
    {
        var currentRetry = 0;

        while (currentRetry < maxRetries)
        {
            // Create the process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = batchFilePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Arguments = string.Join(" ", arguments)
            };

            using var process = new Process();
            process.StartInfo = startInfo;

            var taskCompletionSource = new TaskCompletionSource<bool>();
            process.Exited += (_, _) => taskCompletionSource.SetResult(true);
            process.EnableRaisingEvents = true;

            process.Start();

            var completedTask = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(timeoutMinutes * 1000 * 60));

            if (completedTask == taskCompletionSource.Task)
            {
                Output.Add($"Script completed with exit code: {process.ExitCode}");
                break;
            }

            Errors.Add($"Timeout: Script did not finish within {timeoutMinutes} minutes. Retrying...");
            process.Kill();
            currentRetry++;

            if (currentRetry >= maxRetries)
            {
                Output.Add("Max retries reached. Exiting.");
                break;
            }
        }
    }
}