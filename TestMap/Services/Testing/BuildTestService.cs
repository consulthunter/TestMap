using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.Serialization;
using TestMap.Models;
using TestMap.Models.Coverage;
using TestMap.Models.Results;
using TestMap.Services.Database;

namespace TestMap.Services.Testing;

public class BuildTestService : IBuildTestService
{
    private readonly ProjectModel _projectModel;
    private readonly string _containerName;
    private readonly SqliteDatabaseService _sqliteDatabaseService;
    private string runId;
    private string runDate;

    public string? LatestLogPath { get; private set; }
    public List<TrxTestResult> LatestTestResults { get; private set; } = new();
    public string LatestTestResultRaw { get; private set; } = "";
    public string LatestCoverageReportRaw { get; private set; } = "";
    public string LatestMutationReportRaw { get; private set; } = "";
    public string LatestLizardReportRaw { get; private set; } = "";
    public CoverageReport? LatestCoverageReport { get; private set; }
    public bool LatestSuccess { get; private set; }
    public int LatestCoverage { get; private set; }

    public BuildTestService(ProjectModel project, SqliteDatabaseService sqliteDatabaseService)
    {
        _projectModel = project;
        _containerName = _projectModel.RepoName.ToLower() + "-testing";
        _sqliteDatabaseService = sqliteDatabaseService;
        runId = "";
        runDate = "";
    }

    public async Task<TestRunResult> BuildTestAsync(List<string> solutions, bool isBaseline, string? methodName = null)
    {
        // reset per-run state
        LatestLogPath = null;
        LatestTestResults = new List<TrxTestResult>();
        LatestCoverageReport = null;
        LatestSuccess = false;
        LatestCoverage = 0;
        string desc = "";

        runId = (isBaseline ? "baseline_" : "") + Guid.NewGuid();
        runDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        await _sqliteDatabaseService.TestRunRepository.InsertTestRun(runId, runDate, "started", 0, null, null, null);

        // generated test runs include target method
        var result = isBaseline ? new TestRunResult() : new GeneratedTestRunResult();

        try
        {
            if (isBaseline)
            {
                // solutions is List<string> with full paths
                var solutionFilenames = solutions.Select(Path.GetFileName).ToList();

                // join into comma-separated string
                var allSolutions = string.Join(",", solutionFilenames);

                await RunDockerContainerAsync(allSolutions);
                await WaitForContainerExitAsync(_containerName);
                await CaptureContainerLogsAsync();
                await ProcessTrxResults();
                await LoadMergedCoverageReport();
                await LoadMutationReport(solutionFilenames);
                await LoadLizardReport();
                
                if (LatestCoverageReport != null)
                    LatestCoverage = (int)(LatestCoverageReport.LineRate * 100);
                
                result.Success = true;
                result.RunId = runId;
                result.RunDate = runDate;
                result.Coverage = LatestCoverage;
                result.LogPath = LatestLogPath;
                result.Results = LatestTestResults;
            }
            else
            {
                var solutionFilenames = solutions.Select(Path.GetFileName).ToList();

                var solution = solutionFilenames.First() ?? "";

                await RunDockerContainerAsync(solution);
                await WaitForContainerExitAsync(_containerName);
                await ProcessTrxResults();
                await CaptureContainerLogsAsync();
                await LoadMergedCoverageReport();
                await LoadMutationReport(solutionFilenames);
                await LoadLizardReport();

                result.Success = true;

                if (LatestCoverageReport != null)
                    LatestCoverage = (int)(LatestCoverageReport.LineRate * 100);

                if (result is GeneratedTestRunResult gen)
                {
                    gen.CoveredMethod = methodName;

                    var coverageLookup = LatestCoverageReport?.Packages?
                        .SelectMany(p => p.Classes)
                        .SelectMany(c => c.Methods)
                        .Where(m =>
                            m.Name != ".ctor" &&
                            !m.Name.StartsWith("get_") &&
                            !m.Name.StartsWith("set_"))
                        .GroupBy(m => m.Name)
                        .ToDictionary(g => g.Key, g => g.First());

                    gen.MethodCoverage =
                        coverageLookup?.GetValueOrDefault(methodName)?.LineRate ?? 0;
                }
                
                result.RunId = runId;
                result.RunDate = runDate;
                result.Coverage = LatestCoverage;
                result.LogPath = LatestLogPath;
                result.Results = LatestTestResults;
            }

            if (LatestCoverage > 0)
            {
                desc = "Success: Coverage Found";
            }
            else
            {
                desc = "Failed: Zero Coverage Found";
            }

            await _sqliteDatabaseService.TestRunRepository.UpdateTestRunStatus(runId, desc, result.Coverage,
                result.LogPath, null, LatestTestResultRaw);
        }
        catch (Exception ex)
        {
            result.Success = false;

            await _sqliteDatabaseService.TestRunRepository.UpdateTestRunStatus(
                runId, "failed", 0, LatestLogPath, ex.Message, null);
        }
        finally
        {
            CleanupProjectDirectory();
        }

        return result;
    }


    public async Task RunDockerContainerAsync(string solutions)
    {
        var localDir = _projectModel.DirectoryPath!;
        var context = _projectModel.Config.Docker.Context;
        var imageName = _projectModel.Config.Docker.Image;

        if (context.Contains("desktop-windows"))
        {
            var args =
                $"--context {context} run -d --name {_containerName} " +
                $"-v \"{localDir}:C:\\app\\project\" {imageName} " +
                "powershell -NoProfile -ExecutionPolicy Bypass " +
                "-File C:\\app\\scripts\\run_main.ps1 " +
                $"\"{runId}\" \"{solutions}\"";
            await RunProcessAsync("docker", args);
        }
        else
        {
            var args =
                $"--context {context} run -d --name {_containerName} -v \"{localDir}:/app/project\" {imageName} /bin/bash ./scripts/run_main.sh \"{runId}\" \"{solutions}\"";
            await RunProcessAsync("docker", args);
        }
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

    private async Task LoadMergedCoverageReport()
    {
        var source = Path.Combine(_projectModel.DirectoryPath!, "coverage", $"merged_{runId}.cobertura.xml");
        try
        {
            var serializer = new XmlSerializer(typeof(CoverageReport));
            LatestCoverageReportRaw = await File.ReadAllTextAsync(source);
            using var fs = new FileStream(source, FileMode.Open);
            _projectModel.CoverageReport = serializer.Deserialize(fs) as CoverageReport;
            LatestCoverageReport = _projectModel.CoverageReport;
            if (_projectModel.CoverageReport != null)
                await SaveCoverageReport(_projectModel.CoverageReport);
            _projectModel.Logger?.Information("Coverage report loaded successfully.");
        }
        catch (Exception ex)
        {
            _projectModel.Logger?.Error($"Error loading coverage report: {ex.Message}");
            _projectModel.CoverageReport = new CoverageReport();
        }
    }
    
    private async Task LoadMutationReport(List<string> solutions)
    {
        foreach (var solution in solutions)
        {
            string reportDir = $"{solution.Split(".sln")[0]}_{runId}";
            var report = Path.Combine(_projectModel.DirectoryPath!, "mutation", $"{reportDir}", "reports",
                $"mutation-report.json");

            try
            {
                // Read JSON text from file
                string json = await File.ReadAllTextAsync(report);

                // Deserialize
                var result = JsonSerializer.Deserialize<StrykerMutationResults>(json);
                LatestMutationReportRaw = json;
                //
                await SaveMutationReport(result ?? new StrykerMutationResults());
                // need to calculate score, map 
                _projectModel.Logger?.Information("Mutation report loaded successfully.");
            }
            catch (Exception ex)
            {
                _projectModel.Logger?.Error($"Error loading Mutation report: {ex.Message}");
            }
        }
    }
    
    private async Task LoadLizardReport()
    {
        var report = Path.Combine(_projectModel.DirectoryPath!, "lizard",
            $"lizard_{runId}.xml");

        try
        {
            // Read JSON text from file
            var doc = XDocument.Load(report);
            LatestLizardReportRaw = doc.ToString();

            var functionMetrics = ParseFunctions(doc);
            var fileMetrics     = ParseFiles(doc);
            
            await SaveLizardReport(functionMetrics, fileMetrics);

            _projectModel.Logger?.Information("Lizard report loaded successfully.");
        }
        catch (Exception ex)
        {
            _projectModel.Logger?.Error($"Error loading Lizard report: {ex.Message}");
        }
    }

    private void CleanupProjectDirectory()
    {
        var coverageDir = Path.Combine(_projectModel.DirectoryPath!, "coverage");
        var mutationDir = Path.Combine(_projectModel.DirectoryPath!, "mutation");
        var lizardDir = Path.Combine(_projectModel.DirectoryPath!, "lizard");
        if (Directory.Exists(coverageDir))
            try
            {
                Directory.Delete(coverageDir, true);
                _projectModel.Logger?.Information($"Coverage directory '{coverageDir}' deleted successfully.");
            }
            catch (Exception ex)
            {
                _projectModel.Logger?.Warning($"Failed to delete coverage directory '{coverageDir}': {ex.Message}");
            }
        
        if (Directory.Exists(mutationDir))
            try
            {
                Directory.Delete(mutationDir, true);
                _projectModel.Logger?.Information($"Mutation directory '{mutationDir}' deleted successfully.");
            }
            catch (Exception ex)
            {
                _projectModel.Logger?.Warning($"Failed to delete mutation directory '{mutationDir}': {ex.Message}");
            }
        
        if (Directory.Exists(lizardDir))
            try
            {
                Directory.Delete(lizardDir, true);
                _projectModel.Logger?.Information($"Lizard directory '{lizardDir}' deleted successfully.");
            }
            catch (Exception ex)
            {
                _projectModel.Logger?.Warning($"Failed to delete lizard directory '{lizardDir}': {ex.Message}");
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

            LatestTestResults = results;

            _projectModel.TestResults.AddRange(results);
            foreach (var result in results)
            {
                var methodId = await _sqliteDatabaseService.MethodRepository.FindMethodFromContains(result.TestName);
                result.MethodId = methodId;
            }

            // Filter out results with missing method
            var validResults = results.Where(r => r.MethodId != 0).ToList();
            var invalidResults = results.Where(r => r.MethodId == 0).ToList();

            // Add only valid results to project model
            _projectModel.TestResults.AddRange(validResults);

            // Optionally log missing methods
            foreach (var result in invalidResults)
            {
                _projectModel.Logger?.Warning($"Test method not found in DB, skipping TestResult: {result.TestName}");
            }

            // Insert only valid results
            await _sqliteDatabaseService.TestResultRepository.InsertTestResults(validResults);
        }
    }

    private List<TrxTestResult> ParseTrxFile(string trxFilePath)
    {
        var results = new List<TrxTestResult>();
        var doc = XDocument.Load(trxFilePath);
        LatestTestResultRaw = doc.ToString();
        var ns = doc.Root?.Name.Namespace ?? "";

        var unitTestResults = doc.Descendants(ns + "UnitTestResult");

        foreach (var result in unitTestResults)
        {
            var testName = (string?)result.Attribute("testName") ?? "";
            var outcome = (string?)result.Attribute("outcome") ?? "";
            var durationStr = (string?)result.Attribute("duration") ?? "00:00:00";
            TimeSpan.TryParse(durationStr, out var duration);

            var errorMessage = result
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
    
    public IReadOnlyList<LizardFunctionMetrics> ParseFunctions(XDocument doc)
    {
        var measure =
            doc.Root?
               .Elements("measure")
               .FirstOrDefault(m => (string?)m.Attribute("type") == "Function");

        if (measure == null)
            return Array.Empty<LizardFunctionMetrics>();

        var results = new List<LizardFunctionMetrics>();

        foreach (var item in measure.Elements("item"))
        {
            var nameAttr = (string?)item.Attribute("name");
            if (string.IsNullOrWhiteSpace(nameAttr))
                continue;

            // Split "Func(...) at /path/file.cs:14"
            var atIndex = nameAttr.LastIndexOf(" at ", StringComparison.Ordinal);
            if (atIndex < 0)
                continue;

            var functionName = nameAttr[..atIndex];
            var location = nameAttr[(atIndex + 4)..];

            var colonIndex = location.LastIndexOf(':');
            if (colonIndex < 0)
                continue;

            var filePath = location[..colonIndex];
            var lineNumber = int.Parse(location[(colonIndex + 1)..]);

            var values = item.Elements("value").Select(v => int.Parse(v.Value)).ToArray();
            if (values.Length < 3)
                continue;

            results.Add(new LizardFunctionMetrics
            {
                FunctionName = functionName,
                FilePath = filePath,
                LineNumber = lineNumber,
                Ncss = values[1],
                Ccn = values[2]
            });
        }

        return results;
    }

    public IReadOnlyList<LizardFileMetrics> ParseFiles(XDocument doc)
    {
        var measure =
            doc.Root?
               .Elements("measure")
               .FirstOrDefault(m => (string?)m.Attribute("type") == "File");

        if (measure == null)
            return Array.Empty<LizardFileMetrics>();

        var results = new List<LizardFileMetrics>();

        foreach (var item in measure.Elements("item"))
        {
            var filePath = (string?)item.Attribute("name");
            if (string.IsNullOrWhiteSpace(filePath))
                continue;

            var values = item.Elements("value").Select(v => int.Parse(v.Value)).ToArray();
            if (values.Length < 4)
                continue;

            results.Add(new LizardFileMetrics
            {
                FilePath = filePath,
                Ncss = values[1],
                Ccn = values[2],
                Functions = values[3]
            });
        }

        return results;
    }

    private async Task CaptureContainerLogsAsync()
    {
        var containerName = _containerName ?? throw new InvalidOperationException("Container name is null.");
        var logsFilePath =
            (_projectModel.LogsFilePath ?? throw new InvalidOperationException("LogsFilePath is null!")).Replace(".log",
                "") + $"-docker-{runId}.log";

        _projectModel.Logger?.Information($"Capturing logs for container: {containerName}");

        var logs = await RunProcessAsync("docker", $"logs --since 24h {containerName}");

        await File.WriteAllTextAsync(logsFilePath, logs);

        LatestLogPath = logsFilePath;

        _projectModel.Logger?.Information($"Docker container logs written to: {logsFilePath}");

        await RunProcessAsync("docker", $"rm {containerName}");
        _projectModel.Logger?.Information($"Container '{containerName}' removed.");
    }

    private async Task SaveCoverageReport(CoverageReport coverageReport)
    {
        var reportId =
            await _sqliteDatabaseService.CoverageReportRepository
                .InsertCoverageReportGetId(
                    coverageReport, runId, LatestCoverageReportRaw);

        foreach (var package in coverageReport.Packages)
        {

            var packCoverId =
                await _sqliteDatabaseService.PackageCoverageRepository
                    .InsertPackageCoverageGetId(
                        package, reportId);

            foreach (var claCov in package.Classes)
            {
                var claId =
                    await _sqliteDatabaseService.ClassRepository
                        .FindClass(claCov.Name);

                if (claId == 0)
                {
                    _projectModel.Logger?.Warning(
                        $"Coverage class '{claCov.Name}' not found in DB.");
                    continue;
                }

                var claCoverId =
                    await _sqliteDatabaseService.ClassCoverageRepository
                        .InsertClassCoverageGetId(
                            claCov, packCoverId, claId);

                foreach (var methodCov in claCov.Methods)
                {
                    if (methodCov.Name.Contains("get") ||
                        methodCov.Name.Contains("set") ||
                        methodCov.Name.Contains(".ctor"))
                        continue;

                    var methodId =
                        await _sqliteDatabaseService.MethodRepository
                            .FindMethodFromExact(methodCov.Name);

                    if (methodId == 0)
                    {
                        _projectModel.Logger?.Warning(
                            $"Coverage method '{methodCov.Name}' not found in DB.");
                        continue;
                    }

                    await _sqliteDatabaseService.MethodCoverageRepository
                        .InsertMethodCoverageGetId(
                            methodCov, claCoverId, methodId);
                }
            }
        }
    }

    private async Task SaveMutationReport(StrykerMutationResults mutationReport)
    {
        var reportId = await _sqliteDatabaseService.MutationReportRepository.InsertMutationReport(runId,
            runDate, mutationReport.projectRoot, mutationReport.schemaVersion, LatestMutationReportRaw);
        
        var testMap = new Dictionary<string, string>();

        foreach (var testFile in mutationReport.testFiles)
        {
            foreach (var test in testFile.Value.tests)
            {
                testMap.Add(test.id, test.name);
            }
        }

        foreach (var fileResult in mutationReport.files)
        {
            var score = CalculateMutationScore(fileResult.Value).Score * 100;
            var name = fileResult.Key.Split("/").Last();
            var relativePath = fileResult.Key.Split("/app/project/").Last();
            var normalizedPath = relativePath
                .Replace('/', Path.DirectorySeparatorChar);
            var sourceFileId = await _sqliteDatabaseService.SourceFileRepository.FindSourceFile(name, normalizedPath);

            if (sourceFileId != 0)
            {
                var fileResultId = await _sqliteDatabaseService.FileMutationResultRepository.InsertFileMutationResult(reportId,
                    sourceFileId, fileResult.Value.language, score);
                if (fileResultId != 0)
                {
                    foreach (var mutant in fileResult.Value.mutants)
                    {
                        var methodId =
                            await _sqliteDatabaseService.MethodRepository.FindMethodFromLocation(sourceFileId,
                                mutant.location);
                        if (methodId != 0)
                        {
                            var mutantId =
                                await _sqliteDatabaseService.MutantRepository.InsertMutant(fileResultId, methodId,
                                    mutant);
                            if (mutantId != 0)
                            {
                                foreach (var covered in mutant.coveredBy)
                                {
                                    var testMethodId =
                                        await _sqliteDatabaseService.MethodRepository.FindMethodFromContains(
                                            testMap[covered].Split(".").Last());
                                    if (testMethodId != 0)
                                    {
                                        var mapId =
                                            await _sqliteDatabaseService.MutantTestMapRepository.InsertMutantTestMap(
                                                mutantId, testMethodId, "CoveredBy");
                                    }
                                    else
                                    {
                                        _projectModel.Logger?.Warning($"Test method '{testMap[covered]}' not found in database.");
                                    }
                                }

                                foreach (var killed in mutant.killedBy)
                                {
                                    var testMethodId =
                                        await _sqliteDatabaseService.MethodRepository.FindMethodFromContains(
                                            testMap[killed].Split(".").Last());
                                    if (testMethodId != 0)
                                    {
                                        var mapId =
                                            await _sqliteDatabaseService.MutantTestMapRepository.InsertMutantTestMap(
                                                mutantId, testMethodId, "KilledBy");
                                    }
                                    else
                                    {
                                        _projectModel.Logger?.Warning($"Test method '{testMap[killed]}' not found in database.");
                                    }
                                }
                            }
                            else
                            {
                                _projectModel.Logger?.Warning($"Mutant '{mutant.location}' not found in database.");
                            }
                        }
                        else
                        {
                            _projectModel.Logger?.Warning($"Method mutation result '{mutant.location}' not found in database.");
                        }
                    }
                }
                else
                {
                    _projectModel.Logger?.Warning($"File mutation result '{name}' not found in database.");
                }
            }
            else
            {
                _projectModel.Logger?.Warning($"Source file '{name}' not found in database.");
            }
        }
    }

    public async Task SaveLizardReport(IReadOnlyList<LizardFunctionMetrics> functionMetrics,
        IReadOnlyList<LizardFileMetrics> fileMetrics)
    {
        foreach (var metric in functionMetrics)
        {
            var functionName = metric.FunctionName.Split("::").Last().Split("(").First();
            var methodId = await _sqliteDatabaseService.MethodRepository.FindMethodFromExact(functionName);

            if (methodId != 0)
            {
                var metricId = await _sqliteDatabaseService.LizardFunctionCodeMetricsRepository
                    .InsertLizardFunctionCodeMetric(
                        runId, methodId, metric);
            }
            else
            {
                _projectModel.Logger?.Warning($"Function '{functionName}' not found in database.");
            }
            
        }
        
        foreach (var metric in fileMetrics)
        {
            var filePath = metric.FilePath.Split("/").Last();
            
            var sourceFileId = await _sqliteDatabaseService.SourceFileRepository.FindSourceFile(filePath, filePath);
            if (sourceFileId != 0)
            {
                var metricId = await _sqliteDatabaseService.LizardFileCodeMetricsRepository
                    .InsertLizardFileCodeMetric(
                        runId, sourceFileId, metric);
            }
            else
            {
                _projectModel.Logger?.Warning($"File '{filePath}' not found in database.");
            }
        }
    }

    private StrykerMutationScoreResult CalculateMutationScore(StrykerFileResult fileResult)
    {
        var result = new StrykerMutationScoreResult();

        foreach (var mutant in fileResult.mutants)
        {
            var status = mutant.status?.ToLowerInvariant();

            // Always count ignored + total, but exclude from score
            if (status == "ignored")
            {
                result = result with { Ignored = result.Ignored + 1 };
                continue;
            }

            // Exclude static mutants from score math
            if (mutant.@static)
                continue;

            switch (status)
            {
                case "killed":
                    result = result with { Killed = result.Killed + 1 };
                    break;

                case "survived":
                    result = result with { Survived = result.Survived + 1 };
                    break;

                case "timeout":
                    result = result with { Timeout = result.Timeout + 1 };
                    break;

                case "nocoverage":
                    result = result with { NoCoverage = result.NoCoverage + 1 };
                    break;

                case "compileerrors":
                    result = result with { CompileErrors = result.CompileErrors + 1 };
                    break;
            }
        }

        return result;
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

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();
        await Task.WhenAll(stdoutTask, stderrTask);

        if (process.ExitCode != 0) throw new Exception($"Command failed: {fileName} {arguments}\n{stderrTask.Result}");

        return stdoutTask.Result;
    }
}