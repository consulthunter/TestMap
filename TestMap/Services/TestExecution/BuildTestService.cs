using System.Text.Json;
using System.Text.RegularExpressions;
using TestMap.App;
using TestMap.Models.Code;
using TestMap.Models.Coverage;
using TestMap.Models.Results;
using TestMap.Persistence.Ef.Repositories;
using TestMap.Persistence.Ef.Repositories.Testing;

namespace TestMap.Services.TestExecution;

public class BuildTestService : IBuildTestService
{
    private readonly ProjectContext _context;
    private readonly BuildTestResultCollector _resultCollector;
    private readonly ProjectRepository _projectRepository;
    private readonly TestRunRepository _testRunRepository;
    private readonly TestResultRepository _testResultRepository;
    private readonly CallFailureService _callFailureService;
    private readonly ProjectArtifactCleanupService _artifactCleanupService;
    private readonly DockerRuntimePathMapper _pathMapper;
    private readonly DockerCommandRunner _dockerCommandRunner;
    private readonly string _containerName;
    private string _runId = string.Empty;
    private string _runDate = string.Empty;
    private string? _dockerContextOverride;
    private List<string> _completedMutationTargets = new();
    private IReadOnlyCollection<StrykerMutationResults> _latestMutationReports = [];

    public string? LatestLogPath { get; private set; }
    public List<TestResultModel> LatestTestResults { get; private set; } = new();
    public string LatestTestResultRaw { get; private set; } = "";
    public string LatestCoverageReportRaw { get; private set; } = "";
    public string LatestCoverageReportNormalizedRaw { get; private set; } = "";
    public string LatestMutationReportRaw { get; private set; } = "";
    public double? LatestMutationScore { get; private set; }
    public string? LatestStructuredErrors { get; private set; }
    public CoverageReportModel? LatestCoverageReport { get; private set; }
    public FailureAnalysisModel? LatestFailureAnalysis { get; private set; }
    public bool LatestSuccess { get; private set; }
    public bool LatestRestoreSucceeded { get; private set; }
    public bool LatestBuildSucceeded { get; private set; }
    public bool LatestTestsExecuted { get; private set; }
    public string LatestDockerContext { get; private set; } = string.Empty;
    public string LatestDockerOs { get; private set; } = string.Empty;
    public int LatestCoverage { get; private set; }

    public BuildTestService(
        ProjectContext context,
        BuildTestResultCollector resultCollector,
        ProjectRepository projectRepository,
        TestRunRepository testRunRepository,
        TestResultRepository testResultRepository,
        CallFailureService callFailureService,
        ProjectArtifactCleanupService artifactCleanupService,
        DockerRuntimePathMapper pathMapper,
        DockerCommandRunner dockerCommandRunner)
    {
        _context = context;
        _resultCollector = resultCollector;
        _projectRepository = projectRepository;
        _testRunRepository = testRunRepository;
        _testResultRepository = testResultRepository;
        _callFailureService = callFailureService;
        _artifactCleanupService = artifactCleanupService;
        _pathMapper = pathMapper;
        _dockerCommandRunner = dockerCommandRunner;
        _containerName = _context.Project.RepoName.ToLowerInvariant() + "-testing";
    }

    public async Task<TestRunModel> BuildTestAsync(BuildTestRunRequest request)
    {
        ResetLatestState();
        _artifactCleanupService.CleanupProjectDirectory(false);

        _runId = (request.IsBaseline ? "baseline_" : "iteration_") + Guid.NewGuid();
        _runDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var result = request.IsBaseline ? new TestRunModel() : new GeneratedTestRunModel();
        string? processDiagnostics = null;

        try
        {
            var solutionFilenames = request.Solutions
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(Path.GetFileName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToList();

            if (request.IsBaseline)
            {
                await RunBaselineAsync(solutionFilenames);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.TargetProjectPath))
                    throw new InvalidOperationException("Iteration test runs require a target test project path.");

                await RunDockerTargetedTestsAsync(
                    request.TargetProjectPath,
                    request.TargetFramework,
                    ResolveCoverageCollectorArgument(request.TargetProjectPath));
                await WaitForActiveContainerAsync(true);

                if (!string.IsNullOrWhiteSpace(request.MutationSourceProjectPath))
                {
                    await RunDockerTargetedMutationAsync(
                        request.MutationSourceProjectPath,
                        request.TargetProjectPath);
                    await WaitForActiveContainerAsync(true);
                    _completedMutationTargets.Add(request.MutationSourceProjectPath);
                }
            }

            await CollectAndMapResultsAsync(_completedMutationTargets);
            LatestCoverage = LatestCoverageReport != null ? (int)(LatestCoverageReport.LineRate * 100) : 0;

            if (LatestTestResults.Count == 0 && !string.IsNullOrWhiteSpace(LatestLogPath))
                LatestFailureAnalysis = await AnalyzeFailureFromLogsAsync();

            if (LatestFailureAnalysis == null &&
                LatestTestResults.Count == 0 &&
                LatestCoverageReport == null &&
                !string.IsNullOrWhiteSpace(LatestLogPath))
                LatestFailureAnalysis = new FailureAnalysisModel
                {
                    Stage = "test",
                    Category = "missing_test_artifacts",
                    Summary = "The container run did not produce test results or coverage artifacts.",
                    RemediationSuggestion =
                        "Inspect the stored docker log for test host, collector, or target framework failures.",
                    Evidence = await ReadLatestLogAsync(CancellationToken.None) ?? string.Empty,
                    Source = "runner",
                    Confidence = 0.8
                };
        }
        catch (ProcessExecutionException ex)
        {
            processDiagnostics = ex.ToDiagnosticText();
            await TryCaptureContainerLogsAsync();
            await EnsureDiagnosticLogAsync(processDiagnostics);
            LatestFailureAnalysis = await AnalyzeFailureAsync(processDiagnostics);
            _context.Project.Logger?.Error(ex, "Build/test process execution failed.");
        }
        catch (Exception ex)
        {
            processDiagnostics = ex.ToString();
            await TryCaptureContainerLogsAsync();
            await EnsureDiagnosticLogAsync(processDiagnostics);
            LatestFailureAnalysis = await AnalyzeFailureAsync(processDiagnostics);
            _context.Project.Logger?.Error(ex, "Build/test execution failed.");
        }
        finally
        {
            LatestSuccess = DetermineSuccess();

            result.RunId = _runId;
            result.RunDate = _runDate;
            result.Coverage = LatestCoverage;
            result.MutationScore = LatestMutationScore;
            result.LogPath = LatestLogPath ?? string.Empty;
            result.Results = LatestTestResults;
            result.Success = LatestSuccess;
            result.FailureAnalysis = LatestFailureAnalysis;

            if (result is GeneratedTestRunModel generated)
            {
                generated.CoveredMethod = request.CoveredMethodName ?? string.Empty;
                generated.MethodCoverage = ResolveMethodCoverage(request.CoveredMethodName);
            }

            var testRunId = await _testRunRepository.InsertOrUpdateAsync(result, _context.Project.DbId);
            if (LatestTestResults.Count > 0) await _testResultRepository.InsertAsync(LatestTestResults, testRunId);
            if (_latestMutationReports.Count > 0)
                await _resultCollector.PersistMutationReportsAsync(testRunId, _latestMutationReports);

            var analyzedCommit = _context.Project.Commit ?? _context.CurrentCommit;
            if (!string.IsNullOrWhiteSpace(analyzedCommit))
            {
                _context.Project.LastAnalyzedCommit = analyzedCommit;
                await _projectRepository.InsertOrUpdateAsync(_context.Project);
            }

            _artifactCleanupService.CleanupProjectDirectory(ShouldPreserveArtifactsAfterRun());
        }

        return result;
    }

    public async Task<DockerCompilationValidationResult> ValidateBuildAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
            return DockerCompilationValidationResult.Failure($"Test project file was not found: {projectPath}");

        ResetLatestState();
        _artifactCleanupService.CleanupProjectDirectory(false);
        _runId = $"compile_{Guid.NewGuid()}";
        _runDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        try
        {
            await RunDockerDotnetAsync(
                [
                    "build",
                    GetContainerPath(projectPath),
                    "--nologo"
                ],
                Path.GetDirectoryName(projectPath));

            try
            {
                await WaitForContainerExitAsync(_containerName);
            }
            finally
            {
                await TryCaptureContainerLogsAsync();
            }

            LatestStructuredErrors = null;
            return DockerCompilationValidationResult.Success(await ReadLatestLogAsync(cancellationToken));
        }
        catch (ProcessExecutionException ex)
        {
            await TryCaptureContainerLogsAsync();
            await EnsureDiagnosticLogAsync(ex.ToDiagnosticText());
            LatestStructuredErrors = SerializeBuildDiagnostics(
                await ReadLatestLogAsync(cancellationToken) ?? ex.ToDiagnosticText());
            return DockerCompilationValidationResult.Failure(
                await ReadLatestLogAsync(cancellationToken) ?? ex.ToDiagnosticText(),
                LatestStructuredErrors);
        }
        catch (Exception ex)
        {
            await TryCaptureContainerLogsAsync();
            await EnsureDiagnosticLogAsync(ex.ToString());
            LatestStructuredErrors = SerializeBuildDiagnostics(
                await ReadLatestLogAsync(cancellationToken) ?? ex.ToString());
            return DockerCompilationValidationResult.Failure(
                await ReadLatestLogAsync(cancellationToken) ?? ex.ToString(),
                LatestStructuredErrors);
        }
    }

    private bool ShouldPreserveArtifactsAfterRun()
    {
        return !LatestSuccess || _context.Project.Config.RuntimeConfig.Project.KeepProjectFiles;
    }

    public async Task RunDockerContainerAsync(string solutions)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;
        var quotedRunId = QuoteDockerArgument(_runId);
        var quotedSolutions = QuoteDockerArgument(solutions);

        await _dockerCommandRunner.EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        if (_pathMapper.IsWindowsContext(context))
        {
            var args =
                $"--context {context} run -d --name {_containerName} " +
                $"{mount} {imageName} " +
                $"{DockerRuntimePathMapper.WindowsPythonCommand} -m testmap_runner main --run-id {quotedRunId} --solutions {quotedSolutions} --include-stryker";
            await _dockerCommandRunner.RunProcessAsync("docker", args);
        }
        else
        {
            var args =
                $"--context {context} run -d --name {_containerName} {mount} {imageName} python3 -m testmap_runner main --run-id {quotedRunId} --solutions {quotedSolutions}";
            await _dockerCommandRunner.RunProcessAsync("docker", args);
        }
    }

    private async Task RunDockerTargetedTestsAsync(string testProjectPath, string? targetFramework, string? collector)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;

        await _dockerCommandRunner.EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        var args = BuildTestDockerCommandFactory.CreateTargetedTestsArgs(
            context,
            _containerName,
            mount,
            imageName,
            _runId,
            GetContainerPath(testProjectPath),
            targetFramework,
            collector,
            WindowsNetwork);
        await _dockerCommandRunner.RunProcessAsync("docker", args);
    }

    private async Task RunBaselineAsync(List<string> solutionFilenames)
    {
        if (solutionFilenames.Count == 0)
            throw new InvalidOperationException("Baseline test runs require at least one solution.");

        var baselineFramework = TryResolveCommonBaselineTestFramework(solutionFilenames);
        var requiresWindows = SolutionSetRequiresWindows(solutionFilenames);
        var previousDockerContextOverride = _dockerContextOverride;
        _dockerContextOverride = ResolveDockerContext(requiresWindows);

        if (string.IsNullOrWhiteSpace(baselineFramework))
            _context.Project.Logger?.Information(
                "Baseline solution run selected Docker context '{DockerContext}' without a common target framework; falling back to solution-level dotnet test.",
                CurrentDockerContext);
        else
            _context.Project.Logger?.Information(
                "Baseline solution run selected Docker context '{DockerContext}' and target framework '{TargetFramework}'.",
                CurrentDockerContext,
                baselineFramework);

        try
        {
            LatestDockerContext = CurrentDockerContext;
            LatestDockerOs = ResolveDockerOs(LatestDockerContext);

            await RunDockerDotnetBuildForSolutionsAsync(solutionFilenames);
            await WaitForActiveContainerAsync();
            LatestRestoreSucceeded = true;
            LatestBuildSucceeded = true;

            await RunDockerBaselineTestsAsync(solutionFilenames, baselineFramework);
            await WaitForActiveContainerAsync(true);
            LatestTestsExecuted = true;

            await RunDockerBaselineMutationAsync(solutionFilenames);
            await WaitForActiveContainerAsync(true);
            _completedMutationTargets.AddRange(solutionFilenames);
        }
        finally
        {
            _dockerContextOverride = previousDockerContextOverride;
        }
    }

    private async Task RunDockerBaselineTestsAsync(List<string> solutionFilenames, string? targetFramework)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;

        await _dockerCommandRunner.EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        var args = BuildTestDockerCommandFactory.CreateBaselineTestsArgs(
            context,
            _containerName,
            mount,
            imageName,
            _runId,
            solutionFilenames,
            targetFramework,
            WindowsNetwork);
        await _dockerCommandRunner.RunProcessAsync("docker", args);
    }

    private async Task RunDockerBaselineMutationAsync(List<string> solutionFilenames)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;

        await _dockerCommandRunner.EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        var args = BuildTestDockerCommandFactory.CreateBaselineMutationArgs(
            context,
            _containerName,
            mount,
            imageName,
            _runId,
            solutionFilenames,
            WindowsNetwork);
        await _dockerCommandRunner.RunProcessAsync("docker", args);
    }

    private async Task RunDockerDotnetBuildForSolutionsAsync(List<string> solutionFilenames)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;

        await _dockerCommandRunner.EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        var args = BuildTestDockerCommandFactory.CreateBaselineBuildArgs(
            context,
            _containerName,
            mount,
            imageName,
            _runId,
            solutionFilenames,
            WindowsNetwork);
        await _dockerCommandRunner.RunProcessAsync("docker", args);
    }

    private async Task RunDockerTargetedMutationAsync(string sourceProjectPath, string testProjectPath)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;

        await _dockerCommandRunner.EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        var args = BuildTestDockerCommandFactory.CreateTargetedMutationArgs(
            context,
            _containerName,
            mount,
            imageName,
            _runId,
            GetContainerPath(sourceProjectPath),
            GetContainerPath(testProjectPath),
            WindowsNetwork);
        await _dockerCommandRunner.RunProcessAsync("docker", args);
    }

    private async Task<int> WaitForActiveContainerAsync(bool allowNonZeroExit = false)
    {
        try
        {
            return await WaitForContainerExitAsync(_containerName, allowNonZeroExit);
        }
        finally
        {
            await TryCaptureContainerLogsAsync();
        }
    }

    private async Task RunDockerDotnetAsync(IReadOnlyList<string> dotnetArgs, string? workingDirectory = null)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;

        await _dockerCommandRunner.EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        var args = BuildTestDockerCommandFactory.CreateDotnetPassthroughArgs(
            context,
            _containerName,
            mount,
            imageName,
            dotnetArgs,
            string.IsNullOrWhiteSpace(workingDirectory) ? null : GetContainerPath(workingDirectory),
            WindowsNetwork);
        await _dockerCommandRunner.RunProcessAsync("docker", args);
    }

    private string GetContainerPath(string hostPath)
    {
        return _pathMapper.GetContainerPath(hostPath, _context.Project.DirectoryPath!, CurrentDockerContext);
    }

    private static string QuoteDockerArgument(string value)
    {
        return DockerCommandRunner.QuoteDockerArgument(value);
    }

    private void ResetLatestState()
    {
        LatestLogPath = null;
        LatestTestResults = new List<TestResultModel>();
        LatestTestResultRaw = string.Empty;
        LatestCoverageReportRaw = string.Empty;
        LatestCoverageReportNormalizedRaw = string.Empty;
        LatestMutationReportRaw = string.Empty;
        LatestMutationScore = null;
        LatestStructuredErrors = null;
        LatestCoverageReport = null;
        LatestFailureAnalysis = null;
        LatestSuccess = false;
        LatestRestoreSucceeded = false;
        LatestBuildSucceeded = false;
        LatestTestsExecuted = false;
        LatestDockerContext = CurrentDockerContext;
        LatestDockerOs = ResolveDockerOs(LatestDockerContext);
        LatestCoverage = 0;
        _completedMutationTargets = new List<string>();
        _latestMutationReports = [];
    }

    private async Task<int> WaitForContainerExitAsync(string containerName, bool allowNonZeroExit = false)
    {
        _context.Project.Logger?.Information("Waiting for container '{ContainerName}' to exit...", containerName);
        var context = CurrentDockerContext;

        while (true)
        {
            var inspection = await _dockerCommandRunner.RunProcessAllowFailureAsync(
                "docker",
                $"--context {context} inspect --format=\"{{{{.State.Status}}}}\" {containerName}");
            var status = inspection.StdOut.Trim().Trim('"');

            if (inspection.ExitCode != 0)
                throw new ProcessExecutionException(
                    "docker",
                    $"--context {context} inspect --format=\"{{{{.State.Status}}}}\" {containerName}",
                    inspection.StdOut,
                    inspection.StdErr,
                    inspection.ExitCode);

            if (status == "exited" || status == "dead") break;

            await Task.Delay(2000);
        }

        var exitCodeResult = await _dockerCommandRunner.RunProcessAllowFailureAsync(
            "docker",
            $"--context {context} inspect --format=\"{{{{.State.ExitCode}}}}\" {containerName}");
        var exitCodeText = exitCodeResult.StdOut.Trim().Trim('"');

        if (exitCodeResult.ExitCode != 0)
            throw new ProcessExecutionException(
                "docker",
                $"--context {context} inspect --format=\"{{{{.State.ExitCode}}}}\" {containerName}",
                exitCodeResult.StdOut,
                exitCodeResult.StdErr,
                exitCodeResult.ExitCode);

        if (int.TryParse(exitCodeText, out var exitCode) && exitCode != 0)
        {
            if (allowNonZeroExit)
            {
                _context.Project.Logger?.Information(
                    "Container '{ContainerName}' exited with code {ExitCode}; continuing so test artifacts can be collected.",
                    containerName,
                    exitCode);
                return exitCode;
            }

            throw new ProcessExecutionException(
                "docker",
                $"container {containerName}",
                $"Container '{containerName}' exited with code {exitCode}.",
                string.Empty,
                exitCode);
        }

        return int.TryParse(exitCodeText, out var parsedExitCode) ? parsedExitCode : 0;
    }

    private async Task CollectAndMapResultsAsync(List<string> mutationTargets)
    {
        var collectedResults = await _resultCollector.CollectAndMapAsync(_runId, _runDate, mutationTargets);
        LatestTestResults = collectedResults.TestResults;
        LatestTestResultRaw = collectedResults.TestResultRaw;
        LatestCoverageReport = collectedResults.CoverageReport;
        LatestCoverageReportRaw = collectedResults.CoverageReportRaw;
            LatestCoverageReportNormalizedRaw = collectedResults.CoverageReportNormalizedRaw;
            LatestMutationReportRaw = collectedResults.MutationReportRaw;
            LatestMutationScore = collectedResults.MutationScore;
            _latestMutationReports = collectedResults.MutationReports;
    }

    private bool DetermineSuccess()
    {
        if (LatestFailureAnalysis != null) return false;

        if (LatestTestResults.Count == 0) return false;

        return LatestTestResults.All(x =>
            !string.Equals(x.Outcome, "Failed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(x.Outcome, "Error", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(x.Outcome, "Aborted", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<FailureAnalysisModel?> AnalyzeFailureFromLogsAsync()
    {
        if (string.IsNullOrWhiteSpace(LatestLogPath) || !File.Exists(LatestLogPath)) return null;

        var logs = await File.ReadAllTextAsync(LatestLogPath);
        return _callFailureService.Analyze(logs);
    }

    private async Task<FailureAnalysisModel?> AnalyzeFailureAsync(string processDiagnostics)
    {
        string? logs = null;
        if (!string.IsNullOrWhiteSpace(LatestLogPath) && File.Exists(LatestLogPath))
            logs = await File.ReadAllTextAsync(LatestLogPath);

        return _callFailureService.Analyze(logs, processDiagnostics);
    }

    private double ResolveMethodCoverage(string? methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName)) return 0;

        var coverageLookup = LatestCoverageReport?.Packages?
            .SelectMany(p => p.Classes)
            .SelectMany(c => c.Methods)
            .Where(m =>
                m.Name != ".ctor" &&
                !m.Name.StartsWith("get_") &&
                !m.Name.StartsWith("set_"))
            .GroupBy(m => m.Name)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return coverageLookup?.GetValueOrDefault(methodName)?.LineRate ?? 0;
    }

    private async Task TryCaptureContainerLogsAsync()
    {
        if (!await ContainerExistsAsync(_containerName)) return;

        await CaptureContainerLogsAsync();
    }

    private async Task<bool> ContainerExistsAsync(string containerName)
    {
        var result = await _dockerCommandRunner.RunProcessAllowFailureAsync(
            "docker",
            $"--context {CurrentDockerContext} inspect {containerName}");
        return result.ExitCode == 0;
    }

    private async Task RemoveContainerIfExistsAsync(string containerName)
    {
        if (!await ContainerExistsAsync(containerName)) return;

        _context.Project.Logger?.Warning(
            "Removing stale container '{ContainerName}' before starting a new run.",
            containerName);
        await _dockerCommandRunner.RunProcessAllowFailureAsync(
            "docker",
            $"--context {CurrentDockerContext} rm -f {containerName}");
    }

    private async Task CaptureContainerLogsAsync()
    {
        var logsFilePath =
            (_context.Project.LogsFilePath ?? throw new InvalidOperationException("LogsFilePath is null.")).Replace(
                ".log",
                "") + $"-docker-{_runId}.log";

        var context = CurrentDockerContext;
        var logs = await _dockerCommandRunner.RunProcessAllowFailureAsync(
            "docker",
            $"--context {context} logs --since 24h {_containerName}");
        var content = string.Join(Environment.NewLine,
            new[] { logs.StdOut, logs.StdErr }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var logEntry = string.Join(
            Environment.NewLine,
            [
                $"===== Docker container logs: {_containerName} ({DateTimeOffset.Now:O}) =====",
                content,
                string.Empty
            ]);
        await File.AppendAllTextAsync(logsFilePath, logEntry);
        LatestLogPath = logsFilePath;

        await _dockerCommandRunner.RunProcessAllowFailureAsync(
            "docker",
            $"--context {context} rm {_containerName}");
    }

    private async Task EnsureDiagnosticLogAsync(string diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(LatestLogPath)) return;

        var diagnosticPath =
            (_context.Project.LogsFilePath ?? throw new InvalidOperationException("LogsFilePath is null.")).Replace(
                ".log",
                "") + $"-failure-{_runId}.log";
        await File.WriteAllTextAsync(diagnosticPath, diagnostics);
        LatestLogPath = diagnosticPath;
    }

    private async Task<string?> ReadLatestLogAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(LatestLogPath) || !File.Exists(LatestLogPath)) return null;

        return await File.ReadAllTextAsync(LatestLogPath, cancellationToken);
    }

    private static string? SerializeBuildDiagnostics(string logText)
    {
        var diagnostics = ParseBuildDiagnostics(logText);
        if (diagnostics.Count == 0) return null;

        return JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<BuildDiagnostic> ParseBuildDiagnostics(string logText)
    {
        var diagnostics = new List<BuildDiagnostic>();
        const string pattern =
            @"^(?<file>.*?)(?:\((?<line>\d+)(?:,(?<column>\d+))?\))?\s*:\s*(?<severity>error|warning)\s+(?<code>[A-Z]{2,}\d+)\s*:\s*(?<message>.*?)(?:\s+\[(?<project>[^\]]+)\])?$";

        foreach (var rawLine in logText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            diagnostics.Add(new BuildDiagnostic(
                match.Groups["severity"].Value,
                match.Groups["code"].Value,
                match.Groups["message"].Value.Trim(),
                match.Groups["file"].Value.Trim(),
                ParseNullableInt(match.Groups["line"].Value),
                ParseNullableInt(match.Groups["column"].Value),
                match.Groups["project"].Value.Trim()));
        }

        return diagnostics;
    }

    private static int? ParseNullableInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private IReadOnlyList<BaselineTestTarget> GetBaselineTestTargets()
    {
        return _context.Project.Projects
            .Where(x => x.BuildMetadata.IsTestProject)
            .Select(x => new BaselineTestTarget(
                x.FilePath,
                ChoosePreferredTargetFramework(x),
                ResolveCoverageCollectorArgument(x.BuildMetadata.CoverageCollector)))
            .Distinct()
            .ToList();
    }

    private string? ResolveCoverageCollectorArgument(string projectPath)
    {
        var project = _context.Project.Projects.FirstOrDefault(x =>
            string.Equals(Path.GetFullPath(x.FilePath), Path.GetFullPath(projectPath),
                StringComparison.OrdinalIgnoreCase));
        return ResolveCoverageCollectorArgument(project?.BuildMetadata.CoverageCollector ??
                                                CoverageCollectorType.Unknown);
    }

    private static string ResolveCoverageCollectorArgument(CoverageCollectorType collectorType)
    {
        return BuildTestDockerCommandFactory.ResolveCoverageCollectorArgument(collectorType);
    }

    private string CurrentDockerContext =>
        string.IsNullOrWhiteSpace(_dockerContextOverride)
            ? _context.Project.Config.RuntimeConfig.Docker.Context
            : _dockerContextOverride;

    private string WindowsNetwork =>
        _context.Project.Config.RuntimeConfig.Docker.WindowsNetwork;

    private static string ResolveDockerOs(string dockerContext)
    {
        return dockerContext.Contains(DockerRuntimePathMapper.WindowsContextName, StringComparison.OrdinalIgnoreCase)
            ? "windows"
            : "linux";
    }

    private string ResolveDockerContext(bool requiresWindows)
    {
        return BuildTestDockerCommandFactory.ResolveDockerContext(
            _context.Project.Config.RuntimeConfig.Docker.Context,
            requiresWindows,
            _pathMapper);
    }

    private bool SolutionSetRequiresWindows(List<string> solutionFilenames)
    {
        var solutionProjects = GetProjectsForSolutions(solutionFilenames).ToList();
        return BuildTestDockerCommandFactory.SolutionSetRequiresWindows(solutionProjects);
    }

    private string? TryResolveCommonBaselineTestFramework(List<string> solutionFilenames)
    {
        var solutionProjects = GetProjectsForSolutions(solutionFilenames).ToList();
        var testProjects = solutionProjects.Where(project => project.BuildMetadata.IsTestProject).ToList();

        if (testProjects.Count == 0)
            throw new InvalidOperationException(
                $"No test projects were found for baseline solution(s): {string.Join(", ", solutionFilenames)}.");

        var selectedFramework = BuildTestDockerCommandFactory.TryResolveCommonBaselineTestFramework(solutionProjects);

        if (string.IsNullOrWhiteSpace(selectedFramework))
        {
            var projectTargets = string.Join(
                "; ",
                testProjects.Select(project =>
                    $"{Path.GetFileName(project.FilePath)}=[{string.Join(",", GetProjectTargetFrameworks(project))}]"));
            _context.Project.Logger?.Warning(
                "No common target framework exists across baseline test projects for solution(s) {Solutions}. Falling back to solution-level dotnet test without --framework. {ProjectTargets}",
                string.Join(", ", solutionFilenames),
                projectTargets);
            return null;
        }

        return selectedFramework;
    }

    private IEnumerable<CSharpProjectModel> GetProjectsForSolutions(List<string> solutionFilenames)
    {
        var selectedSolutions = _context.Project.Solutions
            .Where(solution =>
                solutionFilenames.Contains(Path.GetFileName(solution.FilePath), StringComparer.OrdinalIgnoreCase))
            .ToList();

        var projectPaths = selectedSolutions
            .SelectMany(solution => solution.Projects)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _context.Project.Projects
            .Where(project => projectPaths.Contains(project.FilePath));
    }

    private static List<string> GetProjectTargetFrameworks(CSharpProjectModel project)
    {
        return (project.BuildMetadata.BuildTargets.Count > 0
                ? project.BuildMetadata.BuildTargets
                : project.BuildTargets)
            .Where(framework => !string.IsNullOrWhiteSpace(framework))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ChoosePreferredTargetFramework(CSharpProjectModel project)
    {
        return BuildTestDockerCommandFactory.ChoosePreferredTargetFramework(project);
    }

    private sealed record BuildDiagnostic(
        string Severity,
        string Code,
        string Message,
        string File,
        int? Line,
        int? Column,
        string Project);

    private sealed record BaselineTestTarget(string ProjectPath, string? TargetFramework, string Collector);

}

public sealed record DockerCompilationValidationResult(bool IsSuccess, string? LogText, string? StructuredErrors)
{
    public static DockerCompilationValidationResult Success(string? logText)
    {
        return new DockerCompilationValidationResult(true, logText, null);
    }

    public static DockerCompilationValidationResult Failure(string logText, string? structuredErrors = null)
    {
        return new DockerCompilationValidationResult(false, logText, structuredErrors);
    }
}
