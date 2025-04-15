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
    /// <param name="scriptPath">Absolute filepath for the batch file</param>
    public async Task RunPowershellScriptAsync(List<string> arguments, string scriptPath)
    {
        try
        {
            // Build arguments for PowerShell
            string args = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {string.Join(" ", arguments)}";

            // Configure the process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process();
            process.StartInfo = startInfo;

            // Start the process
            process.Start();

            // Capture standard output and error
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            await process.WaitForExitAsync();

            // Print output and error
            Output.AddRange(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
            Errors.AddRange(error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

            // Check exit code
            if (process.ExitCode != 0)
            {
                HasError = true;
                Errors.Add($"Powershell file execution failed with exit code: {process.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            Errors.Add(ex.Message);

        }
    }
}