using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Results;
using TestMap.Services.Database;

namespace TestMap.Services.ProjectOperations;

public class WindowsCheckService: IWindowsCheckService
{
    private ProjectModel _projectModel;
    private TestMapConfig _config;

    public WindowsCheckService(ProjectModel projectModel, TestMapConfig config)
    {
        _projectModel = projectModel;
        _config = config;
    }

    public async Task WindowsCheckAsync()
    {
        // check the db for coverage report, mutation report, etc.
        var check = await CheckLogs();
        
        var result = new WindowsCheckResult(
            _projectModel.RepoName,
            check
        );

        WriteCsvRow(result);
    }

    public async Task<bool> CheckLogs()
    {
        // Normalize the project name for filename matching
        var projectName = _projectModel.RepoName;

        // Pattern: must contain project name AND "docker" AND end with ".log"
        var logFiles = Directory.GetFiles(_config.FilePaths.LogsDirPath, "*.log", SearchOption.AllDirectories)
            .Where(f =>
                Path.GetFileName(f).Contains(projectName, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(f).Contains("docker", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!logFiles.Any())
        {
            Console.WriteLine($"No log files found for project '{projectName}'.");
            return false;
        }

        // Windows-only coverage message
        const string windowsCoverageMessage =
            "Code coverage is currently supported only on Windows.";

        foreach (var logFile in logFiles)
        {
            try
            {
                using var stream = new FileStream(
                    logFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite); // << this is key
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();

                if (content.Contains(windowsCoverageMessage, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Windows-only coverage detected in '{logFile}'.");
                    return true;
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Warning: Could not read log '{logFile}': {ex.Message}");
                // optionally: retry later or skip
            }
        }


        // If no log contains the message, project is Linux-compatible
        return false;
    }

    
    private void WriteCsvRow(WindowsCheckResult result)
    {
        // Parent of the project output directory
        var outputRoot = Directory.GetParent(_projectModel.OutputPath)!.FullName;
        var csvPath = Path.Combine(outputRoot, "windows-check.csv");

        var fileExists = File.Exists(csvPath);

        using var writer = new StreamWriter(csvPath, append: true);

        // Write header once
        if (!fileExists)
        {
            writer.WriteLine(
                "ProjectName,WindowsOnly");
        }

        writer.WriteLine(
            $"{result.ProjectName}," +
            $"{result.WindowsOnly}");
    }
}