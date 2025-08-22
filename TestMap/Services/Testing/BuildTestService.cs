using System.Diagnostics;
using System.Xml.Linq;
using System.Xml.Serialization;
using Serilog;
using TestMap.Models;
using TestMap.Models.Coverage;
using TestMap.Models.Results;
using TestMap.Services.Database;

namespace TestMap.Services.ProjectOperations;

public class BuildTestService : IBuildTestService
{
    private readonly ProjectModel _projectModel;
    private readonly string _containerName;
    private readonly SqliteDatabaseService _sqliteDatabaseService;
    private string runId;
    private string runDate;
    public BuildTestService(ProjectModel project, SqliteDatabaseService sqliteDatabaseService)
    {
        _projectModel = project;
        _containerName = _projectModel.RepoName.ToLower() + "-testing";
        _sqliteDatabaseService = sqliteDatabaseService;
        
    }

    public async Task BuildTestAsync()
    {
        runId = Guid.NewGuid().ToString();
        runDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        try
        {
            await RunDockerContainerAsync();
            await WaitForContainerExitAsync(_containerName);
            await MergeCoverageReportsAsync(Path.Combine(_projectModel.DirectoryPath, "coverage"));
            CopyMergedCoverageReport();
            await ProcessTrxResults();
            await CaptureContainerLogsAsync();
        }
        catch (Exception ex)
        {
            _projectModel.Logger?.Error($"BuildTestAsync failed: {ex.Message}");
        }

        LoadCoverageReport();
        CleanupCoverageDirectory();
    }

    private async Task RunDockerContainerAsync()
    {
        string localDir = _projectModel.DirectoryPath ?? throw new InvalidOperationException("DirectoryPath is null.");
        string outputDir = _projectModel.OutputPath ?? throw new InvalidOperationException("OutputPath is null.");
        string imageName = _projectModel.Docker?["all"] ?? throw new InvalidOperationException("Docker image not specified.");

        if (!Directory.Exists(localDir))
            throw new DirectoryNotFoundException($"Local directory does not exist: {localDir}");

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            _projectModel.Logger?.Information($"Created output directory: {outputDir}");
        }

        var args = $"run -d --name {_containerName} -v \"{localDir}:/app/project\" {imageName} /bin/bash ./scripts/run-dotnet-steps.sh";
        _projectModel.Logger?.Information($"Running Docker container: {args}");

        await RunProcessAsync("docker", args);
    }

    private async Task WaitForContainerExitAsync(string containerName)
    {
        _projectModel.Logger?.Information($"Waiting for container '{containerName}' to exit...");

        while (true)
        {
            var status = await RunProcessAsync("docker", $"inspect --format=\"{{{{.State.Status}}}}\" {containerName}");
            if (status.Trim() == "exited")
                break;

            await Task.Delay(2000);
        }

        _projectModel.Logger?.Information($"Container '{containerName}' has exited.");
    }

    private async Task MergeCoverageReportsAsync(string coverageDir)
    {
        _projectModel.Logger?.Information($"Merging coverage reports in {coverageDir}");

        if (!Directory.Exists(coverageDir))
            throw new DirectoryNotFoundException($"Coverage directory not found: {coverageDir}");

        string args = "merge **.cobertura.xml --output merged.cobertura.xml --output-format cobertura";
        await RunProcessAsync("dotnet-coverage", args, coverageDir);
    }

    private void CopyMergedCoverageReport()
    {
        string source = Path.Combine(_projectModel.DirectoryPath!, "coverage", "merged.cobertura.xml");
        string destination = Path.Combine(_projectModel.OutputPath!, "merged.cobertura.xml");

        if (File.Exists(source))
        {
            File.Copy(source, destination, overwrite: true);
            _projectModel.Logger?.Information($"Copied coverage report to: {destination}");
        }
        else
        {
            _projectModel.Logger?.Warning($"Merged coverage file not found at: {source}");
        }
    }
    
    private void CleanupCoverageDirectory()
    {
        var coverageDir = Path.Combine(_projectModel.DirectoryPath!, "coverage");
        if (Directory.Exists(coverageDir))
        {
            try
            {
                Directory.Delete(coverageDir, recursive: true);
                _projectModel.Logger?.Information($"Coverage directory '{coverageDir}' deleted successfully.");
            }
            catch (Exception ex)
            {
                _projectModel.Logger?.Warning($"Failed to delete coverage directory '{coverageDir}': {ex.Message}");
            }
        }
    }
    
    private async Task ProcessTrxResults()
    {
        var trxDir = Path.Combine(_projectModel.DirectoryPath!, "coverage");
        if (!Directory.Exists(trxDir))
        {
            _projectModel.Logger?.Warning($"TRX results directory not found: {trxDir}");
            return;
        }

        var trxFiles = Directory.GetFiles(trxDir, "*.trx");
        foreach (var trxFile in trxFiles)
        {
            _projectModel.Logger?.Information($"Parsing TRX file: {trxFile}");
            var results = ParseTrxFile(trxFile);

            // Append or replace results — depends on your design intent
            _projectModel.TestResults.AddRange(results);
            foreach (var result in results)
            {
                var methodId = await _sqliteDatabaseService.FindMethod(result.TestName);
                result.MethodId = methodId;
            }
            await _sqliteDatabaseService.InsertTestResults(results);
        }
    }


    private List<TrxTestResult> ParseTrxFile(string trxFilePath)
    {
        var results = new List<TrxTestResult>();
        var doc = XDocument.Load(trxFilePath);
        XNamespace ns = doc.Root?.Name.Namespace ?? "";

        var unitTestResults = doc.Descendants(ns + "UnitTestResult");

        foreach (var result in unitTestResults)
        {
            string testName = (string?)result.Attribute("testName") ?? "";
            string outcome = (string?)result.Attribute("outcome") ?? "";
            string durationStr = (string?)result.Attribute("duration") ?? "00:00:00";
            TimeSpan.TryParse(durationStr, out TimeSpan duration);

            string? errorMessage = result
                .Element(ns + "Output")
                ?.Element(ns + "ErrorInfo")
                ?.Element(ns + "Message")
                ?.Value;

            results.Add(new TrxTestResult
            {
                RunId = runId,
                RunDate = runDate,
                TestName = testName,
                Outcome = outcome,
                Duration = duration,
                ErrorMessage = errorMessage
            });
        }

        return results;
    }


    private async Task CaptureContainerLogsAsync()
    {
        string containerName = _containerName ?? throw new InvalidOperationException("Container name is null.");
        string logsFilePath = (_projectModel.LogsFilePath ?? throw new InvalidOperationException("LogsFilePath is null!")).Replace(".log", "") + "-docker.log";

        _projectModel.Logger?.Information($"Capturing logs for container: {containerName}");

        // Get the logs from the Docker container
        string logs = await RunProcessAsync("docker", $"logs {containerName}");

        // Write logs as plain text to the dedicated Docker logs file
        await File.WriteAllTextAsync(logsFilePath, logs);

        _projectModel.Logger?.Information($"Docker container logs written to: {logsFilePath}");

        // Remove the container
        await RunProcessAsync("docker", $"rm {containerName}");
        _projectModel.Logger?.Information($"Container '{containerName}' removed.");
    }


    private void LoadCoverageReport()
    {
        try
        {
            var coverageFile = Path.Combine(_projectModel.OutputPath ?? "", "merged.cobertura.xml");

            XmlSerializer serializer = new XmlSerializer(typeof(CoverageReport));
            using FileStream fs = new FileStream(coverageFile, FileMode.Open);
            _projectModel.CoverageReport = serializer.Deserialize(fs) as CoverageReport;
            _projectModel.Logger?.Information("Coverage report loaded successfully.");
        }
        catch (Exception ex)
        {
            _projectModel.Logger?.Error($"Error loading coverage report: {ex.Message}");
            _projectModel.CoverageReport = new CoverageReport();
        }
    }

    private async Task<string> RunProcessAsync(string fileName, string arguments, string? workingDir = null)
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

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Command failed: {fileName} {arguments}\n{error}");
        }

        return output;
    }
}
