using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using TestMap.App;
using TestMap.Models.Code;
using TestMap.Models.Coverage;
using TestMap.Models.Results;
using TestMap.Persistence.Ef.Repositories;
using TestMap.Persistence.Ef.Repositories.Testing;
using TestMap.Services.CollectInformation;
using TestMap.Services.Mapping;

namespace TestMap.Services.Testing;

public class BuildTestService : IBuildTestService
{
    private readonly ProjectContext _context;
    private readonly CollectCoverageResultsService _collectCoverageResultsService;
    private readonly CollectMutationTestingResultsService _collectMutationTestingResultsService;
    private readonly CollectTestResultsService _collectTestResultsService;
    private readonly MapCoverageService _mapCoverageService;
    private readonly MapMutationService _mapMutationService;
    private readonly ProjectRepository _projectRepository;
    private readonly TestRunRepository _testRunRepository;
    private readonly TestResultRepository _testResultRepository;
    private readonly CallFailureService _callFailureService;
    private readonly ProjectArtifactCleanupService _artifactCleanupService;
    private readonly DockerRuntimePathMapper _pathMapper;
    private readonly string _containerName;
    private string _runId = string.Empty;
    private string _runDate = string.Empty;
    private string? _dockerContextOverride;
    private List<string> _completedMutationTargets = new();

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
    public int LatestCoverage { get; private set; }

    public BuildTestService(
        ProjectContext context,
        CollectCoverageResultsService collectCoverageResultsService,
        CollectMutationTestingResultsService collectMutationTestingResultsService,
        CollectTestResultsService collectTestResultsService,
        MapCoverageService mapCoverageService,
        MapMutationService mapMutationService,
        ProjectRepository projectRepository,
        TestRunRepository testRunRepository,
        TestResultRepository testResultRepository,
        CallFailureService callFailureService,
        ProjectArtifactCleanupService artifactCleanupService,
        DockerRuntimePathMapper pathMapper)
    {
        _context = context;
        _collectCoverageResultsService = collectCoverageResultsService;
        _collectMutationTestingResultsService = collectMutationTestingResultsService;
        _collectTestResultsService = collectTestResultsService;
        _mapCoverageService = mapCoverageService;
        _mapMutationService = mapMutationService;
        _projectRepository = projectRepository;
        _testRunRepository = testRunRepository;
        _testResultRepository = testResultRepository;
        _callFailureService = callFailureService;
        _artifactCleanupService = artifactCleanupService;
        _pathMapper = pathMapper;
        _containerName = _context.Project.RepoName.ToLowerInvariant() + "-testing";
    }

    public async Task<TestRunModel> BuildTestAsync(BuildTestRunRequest request)
    {
        ResetLatestState();
        _artifactCleanupService.CleanupProjectDirectory(preserveArtifacts: false);

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
                {
                    throw new InvalidOperationException("Iteration test runs require a target test project path.");
                }

                await RunDockerTargetedTestsAsync(
                    request.TargetProjectPath,
                    request.TargetFramework,
                    ResolveCoverageCollectorArgument(request.TargetProjectPath));
                await WaitForActiveContainerAsync(allowNonZeroExit: true);

                if (!string.IsNullOrWhiteSpace(request.MutationSourceProjectPath))
                {
                    await RunDockerTargetedMutationAsync(
                        request.MutationSourceProjectPath,
                        request.TargetProjectPath);
                    await WaitForActiveContainerAsync(allowNonZeroExit: true);
                    _completedMutationTargets.Add(request.MutationSourceProjectPath);
                }
            }

            await CollectAndMapResultsAsync(_completedMutationTargets);
            LatestCoverage = LatestCoverageReport != null ? (int)(LatestCoverageReport.LineRate * 100) : 0;

            if (LatestTestResults.Count == 0 && !string.IsNullOrWhiteSpace(LatestLogPath))
            {
                LatestFailureAnalysis = await AnalyzeFailureFromLogsAsync();
            }

            if (LatestFailureAnalysis == null &&
                LatestTestResults.Count == 0 &&
                LatestCoverageReport == null &&
                !string.IsNullOrWhiteSpace(LatestLogPath))
            {
                LatestFailureAnalysis = new FailureAnalysisModel
                {
                    Stage = "test",
                    Category = "missing_test_artifacts",
                    Summary = "The container run did not produce test results or coverage artifacts.",
                    RemediationSuggestion = "Inspect the stored docker log for test host, collector, or target framework failures.",
                    Evidence = await ReadLatestLogAsync(CancellationToken.None) ?? string.Empty,
                    Source = "runner",
                    Confidence = 0.8
                };
            }
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
            if (LatestTestResults.Count > 0)
            {
                await _testResultRepository.InsertAsync(LatestTestResults, testRunId);
            }

            var analyzedCommit = _context.Project.Commit ?? _context.CurrentCommit;
            if (!string.IsNullOrWhiteSpace(analyzedCommit))
            {
                _context.Project.LastAnalyzedCommit = analyzedCommit;
                await _projectRepository.InsertOrUpdateAsync(_context.Project);
            }

            _artifactCleanupService.CleanupProjectDirectory(preserveArtifacts: !LatestSuccess);
        }

        return result;
    }

    public async Task<DockerCompilationValidationResult> ValidateBuildAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
        {
            return DockerCompilationValidationResult.Failure($"Test project file was not found: {projectPath}");
        }

        ResetLatestState();
        _artifactCleanupService.CleanupProjectDirectory(preserveArtifacts: false);
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

    public async Task RunDockerContainerAsync(string solutions)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;
        var quotedRunId = QuoteDockerArgument(_runId);
        var quotedSolutions = QuoteDockerArgument(solutions);

        await EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        if (_pathMapper.IsWindowsContext(context))
        {
            var args =
                $"--context {context} run -d --name {_containerName} " +
                $"{mount} {imageName} " +
                $"{DockerRuntimePathMapper.WindowsPythonCommand} -m testmap_runner main --run-id {quotedRunId} --solutions {quotedSolutions} --include-stryker";
            await RunProcessAsync("docker", args);
        }
        else
        {
            var args =
                $"--context {context} run -d --name {_containerName} {mount} {imageName} python3 -m testmap_runner main --run-id {quotedRunId} --solutions {quotedSolutions}";
            await RunProcessAsync("docker", args);
        }
    }

    private async Task RunDockerTargetedTestsAsync(string testProjectPath, string? targetFramework, string? collector)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;
        var quotedRunId = QuoteDockerArgument(_runId);
        var quotedProject = QuoteDockerArgument(GetContainerPath(testProjectPath));
        var frameworkArgs = string.IsNullOrWhiteSpace(targetFramework)
            ? string.Empty
            : $" --framework {QuoteDockerArgument(targetFramework)}";
        var collectorArgs = string.IsNullOrWhiteSpace(collector)
            ? string.Empty
            : $" --collector {QuoteDockerArgument(collector)}";

        await EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        if (_pathMapper.IsWindowsContext(context))
        {
            var args =
                $"--context {context} run -d --name {_containerName} " +
                $"{mount} {imageName} " +
                $"{DockerRuntimePathMapper.WindowsPythonCommand} -m testmap_runner dotnet-test-project --run-id {quotedRunId} --project {quotedProject}{frameworkArgs}{collectorArgs}";
            await RunProcessAsync("docker", args);
        }
        else
        {
            var args =
                $"--context {context} run -d --name {_containerName} {mount} {imageName} " +
                $"python3 -m testmap_runner dotnet-test-project --run-id {quotedRunId} --project {quotedProject}{frameworkArgs}{collectorArgs}";
            await RunProcessAsync("docker", args);
        }
    }

    private async Task RunBaselineAsync(List<string> solutionFilenames)
    {
        if (solutionFilenames.Count == 0)
        {
            throw new InvalidOperationException("Baseline test runs require at least one solution.");
        }

        var baselineFramework = ResolveCommonBaselineTestFramework(solutionFilenames);
        var requiresWindows = SolutionSetRequiresWindows(solutionFilenames);
        var previousDockerContextOverride = _dockerContextOverride;
        _dockerContextOverride = ResolveDockerContext(requiresWindows);

        _context.Project.Logger?.Information(
            "Baseline solution run selected Docker context '{DockerContext}' and target framework '{TargetFramework}'.",
            CurrentDockerContext,
            baselineFramework);

        try
        {
            await RunDockerDotnetBuildForSolutionsAsync(solutionFilenames);
            await WaitForActiveContainerAsync();

            await RunDockerBaselineTestsAsync(solutionFilenames, baselineFramework);
            await WaitForActiveContainerAsync(allowNonZeroExit: true);

            await RunDockerBaselineMutationAsync(solutionFilenames);
            await WaitForActiveContainerAsync(allowNonZeroExit: true);
            _completedMutationTargets.AddRange(solutionFilenames);
        }
        finally
        {
            _dockerContextOverride = previousDockerContextOverride;
        }
    }

    private async Task RunDockerBaselineTestsAsync(List<string> solutionFilenames, string targetFramework)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;
        var quotedRunId = QuoteDockerArgument(_runId);
        var quotedSolutions = QuoteDockerArgument(string.Join(",", solutionFilenames));
        var quotedFramework = QuoteDockerArgument(targetFramework);

        await EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        if (_pathMapper.IsWindowsContext(context))
        {
            var args =
                $"--context {context} run -d --name {_containerName} " +
                $"{mount} {imageName} " +
                $"{DockerRuntimePathMapper.WindowsPythonCommand} -m testmap_runner dotnet-tests --run-id {quotedRunId} --solutions {quotedSolutions} --framework {quotedFramework}";
            await RunProcessAsync("docker", args);
        }
        else
        {
            var args =
                $"--context {context} run -d --name {_containerName} {mount} {imageName} " +
                $"python3 -m testmap_runner dotnet-tests --run-id {quotedRunId} --solutions {quotedSolutions} --framework {quotedFramework}";
            await RunProcessAsync("docker", args);
        }
    }

    private async Task RunDockerBaselineMutationAsync(List<string> solutionFilenames)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;
        var quotedRunId = QuoteDockerArgument(_runId);
        var quotedSolutions = QuoteDockerArgument(string.Join(",", solutionFilenames));

        await EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        if (_pathMapper.IsWindowsContext(context))
        {
            var args =
                $"--context {context} run -d --name {_containerName} " +
                $"{mount} {imageName} " +
                $"{DockerRuntimePathMapper.WindowsPythonCommand} -m testmap_runner dotnet-stryker --run-id {quotedRunId} --solutions {quotedSolutions}";
            await RunProcessAsync("docker", args);
        }
        else
        {
            var args =
                $"--context {context} run -d --name {_containerName} {mount} {imageName} " +
                $"python3 -m testmap_runner dotnet-stryker --run-id {quotedRunId} --solutions {quotedSolutions}";
            await RunProcessAsync("docker", args);
        }
    }

    private async Task RunDockerDotnetBuildForSolutionsAsync(List<string> solutionFilenames)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;
        var quotedRunId = QuoteDockerArgument(_runId);
        var quotedSolutions = QuoteDockerArgument(string.Join(",", solutionFilenames));

        await EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        if (_pathMapper.IsWindowsContext(context))
        {
            var args =
                $"--context {context} run -d --name {_containerName} " +
                $"{mount} {imageName} " +
                $"{DockerRuntimePathMapper.WindowsPythonCommand} -m testmap_runner dotnet-build --run-id {quotedRunId} --solutions {quotedSolutions}";
            await RunProcessAsync("docker", args);
        }
        else
        {
            var args =
                $"--context {context} run -d --name {_containerName} {mount} {imageName} " +
                $"python3 -m testmap_runner dotnet-build --run-id {quotedRunId} --solutions {quotedSolutions}";
            await RunProcessAsync("docker", args);
        }
    }

    private async Task RunDockerTargetedMutationAsync(string sourceProjectPath, string testProjectPath)
    {
        var localDir = _context.Project.DirectoryPath!;
        var context = CurrentDockerContext;
        var imageName = _context.Project.Config.RuntimeConfig.Docker.Image;
        var quotedRunId = QuoteDockerArgument(_runId);
        var quotedSourceProject = QuoteDockerArgument(GetContainerPath(sourceProjectPath));
        var quotedTestProject = QuoteDockerArgument(GetContainerPath(testProjectPath));

        await EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        if (_pathMapper.IsWindowsContext(context))
        {
            var args =
                $"--context {context} run -d --name {_containerName} " +
                $"{mount} {imageName} " +
                $"{DockerRuntimePathMapper.WindowsPythonCommand} -m testmap_runner dotnet-stryker-project --run-id {quotedRunId} --project {quotedSourceProject} --test-project {quotedTestProject}";
            await RunProcessAsync("docker", args);
        }
        else
        {
            var args =
                $"--context {context} run -d --name {_containerName} {mount} {imageName} " +
                $"python3 -m testmap_runner dotnet-stryker-project --run-id {quotedRunId} --project {quotedSourceProject} --test-project {quotedTestProject}";
            await RunProcessAsync("docker", args);
        }
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
        var workingDirectoryArg = string.IsNullOrWhiteSpace(workingDirectory)
            ? string.Empty
            : $" --working-directory {QuoteDockerArgument(GetContainerPath(workingDirectory))}";
        var dotnetArgumentText = string.Join(" ", dotnetArgs.Select(QuoteDockerArgument));

        await EnsureDockerContextReadyAsync(context);
        await RemoveContainerIfExistsAsync(_containerName);

        var mount = _pathMapper.GetMountArgument(localDir, context);
        if (_pathMapper.IsWindowsContext(context))
        {
            var args =
                $"--context {context} run -d --name {_containerName} " +
                $"{mount} {imageName} " +
                $"{DockerRuntimePathMapper.WindowsPythonCommand} -m testmap_runner dotnet{workingDirectoryArg} {dotnetArgumentText}";
            await RunProcessAsync("docker", args);
        }
        else
        {
            var args =
                $"--context {context} run -d --name {_containerName} {mount} {imageName} " +
                $"python3 -m testmap_runner dotnet{workingDirectoryArg} {dotnetArgumentText}";
            await RunProcessAsync("docker", args);
        }
    }

    private static string QuoteDockerArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private string GetContainerPath(string hostPath)
    {
        return _pathMapper.GetContainerPath(hostPath, _context.Project.DirectoryPath!, CurrentDockerContext);
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
        LatestCoverage = 0;
        _completedMutationTargets = new List<string>();
    }

    private async Task<int> WaitForContainerExitAsync(string containerName, bool allowNonZeroExit = false)
    {
        _context.Project.Logger?.Information("Waiting for container '{ContainerName}' to exit...", containerName);

        while (true)
        {
            var inspection = await RunProcessAllowFailureAsync("docker", $"inspect --format=\"{{{{.State.Status}}}}\" {containerName}");
            var status = inspection.StdOut.Trim().Trim('"');

            if (inspection.ExitCode != 0)
            {
                throw new ProcessExecutionException("docker", $"inspect --format=\"{{{{.State.Status}}}}\" {containerName}", inspection.StdOut, inspection.StdErr, inspection.ExitCode);
            }

            if (status == "exited" || status == "dead")
            {
                break;
            }

            await Task.Delay(2000);
        }

        var exitCodeResult = await RunProcessAllowFailureAsync("docker", $"inspect --format=\"{{{{.State.ExitCode}}}}\" {containerName}");
        var exitCodeText = exitCodeResult.StdOut.Trim().Trim('"');

        if (exitCodeResult.ExitCode != 0)
        {
            throw new ProcessExecutionException("docker", $"inspect --format=\"{{{{.State.ExitCode}}}}\" {containerName}", exitCodeResult.StdOut, exitCodeResult.StdErr, exitCodeResult.ExitCode);
        }

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
        var (testResults, testResultRaw) = await _collectTestResultsService.CollectAsync(_runId, _runDate);
        LatestTestResults = testResults;
        LatestTestResultRaw = testResultRaw;
        _context.Project.TestResults = testResults;

        try
        {
            var (coverageReport, rawCoverageReport, normalizedCoverageReport) =
                await _collectCoverageResultsService.CollectAsync(_runId);
            LatestCoverageReport = coverageReport;
            LatestCoverageReportRaw = rawCoverageReport;
            LatestCoverageReportNormalizedRaw = normalizedCoverageReport;
            _context.Project.CoverageReport = coverageReport;

            if (coverageReport != null)
            {
                await _mapCoverageService.MapAsync(coverageReport);
            }
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Error(ex, "Coverage collection or mapping failed.");
            throw;
        }

        if (mutationTargets.Count == 0)
        {
            return;
        }

        try
        {
            var (mutationReports, rawMutationReports) =
                await _collectMutationTestingResultsService.CollectAsync(_runId, mutationTargets);
            LatestMutationReportRaw = rawMutationReports;

            var mutationScores = new List<double>();
            foreach (var mutationReport in mutationReports)
            {
                mutationScores.Add(await _mapMutationService.MapAsync(mutationReport));
            }

            LatestMutationScore = mutationScores.Count == 0
                ? null
                : mutationScores.Average();
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Error(ex, "Mutation collection or mapping failed.");
            throw;
        }
    }

    private bool DetermineSuccess()
    {
        if (LatestFailureAnalysis != null)
        {
            return false;
        }

        if (LatestTestResults.Count == 0)
        {
            return false;
        }

        return LatestTestResults.All(x =>
            !string.Equals(x.Outcome, "Failed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(x.Outcome, "Error", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(x.Outcome, "Aborted", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<FailureAnalysisModel?> AnalyzeFailureFromLogsAsync()
    {
        if (string.IsNullOrWhiteSpace(LatestLogPath) || !File.Exists(LatestLogPath))
        {
            return null;
        }

        var logs = await File.ReadAllTextAsync(LatestLogPath);
        return _callFailureService.Analyze(logs);
    }

    private async Task<FailureAnalysisModel?> AnalyzeFailureAsync(string processDiagnostics)
    {
        string? logs = null;
        if (!string.IsNullOrWhiteSpace(LatestLogPath) && File.Exists(LatestLogPath))
        {
            logs = await File.ReadAllTextAsync(LatestLogPath);
        }

        return _callFailureService.Analyze(logs, processDiagnostics);
    }

    private double ResolveMethodCoverage(string? methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return 0;
        }

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
        if (!await ContainerExistsAsync(_containerName))
        {
            return;
        }

        await CaptureContainerLogsAsync();
    }

    private async Task<bool> ContainerExistsAsync(string containerName)
    {
        var result = await RunProcessAllowFailureAsync("docker", $"inspect {containerName}");
        return result.ExitCode == 0;
    }

    private async Task RemoveContainerIfExistsAsync(string containerName)
    {
        if (!await ContainerExistsAsync(containerName))
        {
            return;
        }

        _context.Project.Logger?.Warning(
            "Removing stale container '{ContainerName}' before starting a new run.",
            containerName);
        await RunProcessAllowFailureAsync("docker", $"rm -f {containerName}");
    }

    private async Task CaptureContainerLogsAsync()
    {
        var logsFilePath =
            (_context.Project.LogsFilePath ?? throw new InvalidOperationException("LogsFilePath is null.")).Replace(".log",
                "") + $"-docker-{_runId}.log";

        var logs = await RunProcessAllowFailureAsync("docker", $"logs --since 24h {_containerName}");
        var content = string.Join(Environment.NewLine, new[] { logs.StdOut, logs.StdErr }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var logEntry = string.Join(
            Environment.NewLine,
            [
                $"===== Docker container logs: {_containerName} ({DateTimeOffset.Now:O}) =====",
                content,
                string.Empty
            ]);
        await File.AppendAllTextAsync(logsFilePath, logEntry);
        LatestLogPath = logsFilePath;

        await RunProcessAllowFailureAsync("docker", $"rm {_containerName}");
    }

    private async Task EnsureDiagnosticLogAsync(string diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(LatestLogPath))
        {
            return;
        }

        var diagnosticPath =
            (_context.Project.LogsFilePath ?? throw new InvalidOperationException("LogsFilePath is null.")).Replace(".log",
                "") + $"-failure-{_runId}.log";
        await File.WriteAllTextAsync(diagnosticPath, diagnostics);
        LatestLogPath = diagnosticPath;
    }

    private async Task<string?> ReadLatestLogAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(LatestLogPath) || !File.Exists(LatestLogPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(LatestLogPath, cancellationToken);
    }

    private static string? SerializeBuildDiagnostics(string logText)
    {
        var diagnostics = ParseBuildDiagnostics(logText);
        if (diagnostics.Count == 0)
        {
            return null;
        }

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
            if (!match.Success)
            {
                continue;
            }

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

    private async Task RunProcessAsync(string fileName, string arguments, string? workingDir = null)
    {
        var result = await RunProcessAllowFailureAsync(fileName, arguments, workingDir);
        if (result.ExitCode != 0)
        {
            throw new ProcessExecutionException(fileName, arguments, result.StdOut, result.StdErr, result.ExitCode);
        }
    }

    private async Task EnsureDockerContextReadyAsync(string context)
    {
        var expectedOs = _pathMapper.ResolveExpectedOs(context);

        if (!await DockerContextExistsAsync(context))
        {
            throw new InvalidOperationException($"Docker context '{context}' was not found.");
        }

        await EnsureDockerDesktopStartedAsync();

        if (await IsDockerDaemonAsync(context, expectedOs))
        {
            return;
        }

        if (OperatingSystem.IsWindows() &&
            (_pathMapper.IsWindowsContext(context) ||
             context.Contains(DockerRuntimePathMapper.LinuxContextName, StringComparison.OrdinalIgnoreCase)))
        {
            await SwitchDockerDesktopEngineAsync(expectedOs);
        }

        if (!await WaitForDockerDaemonAsync(context, expectedOs, TimeSpan.FromMinutes(2)))
        {
            throw new InvalidOperationException(
                $"Docker context '{context}' is not ready with a {expectedOs} daemon.");
        }
    }

    private async Task EnsureDockerDesktopStartedAsync()
    {
        if (await IsDockerResponsiveAsync())
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var dockerDesktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Docker",
            "Docker",
            "Docker Desktop.exe");

        if (!File.Exists(dockerDesktopPath))
        {
            return;
        }

        _context.Project.Logger?.Information("Starting Docker Desktop.");
        Process.Start(new ProcessStartInfo
        {
            FileName = dockerDesktopPath,
            UseShellExecute = true
        });

        await WaitForDockerResponsiveAsync(TimeSpan.FromMinutes(2));
    }

    private async Task SwitchDockerDesktopEngineAsync(string expectedOs)
    {
        var dockerCliPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Docker",
            "Docker",
            "DockerCli.exe");

        if (!File.Exists(dockerCliPath))
        {
            return;
        }

        var switchArgument = expectedOs.Equals("windows", StringComparison.OrdinalIgnoreCase)
            ? "-SwitchWindowsEngine"
            : "-SwitchLinuxEngine";

        _context.Project.Logger?.Information("Switching Docker Desktop to {DockerEngine} containers.", expectedOs);
        await RunProcessAllowFailureAsync(dockerCliPath, switchArgument);
    }

    private async Task<bool> DockerContextExistsAsync(string context)
    {
        var result = await RunProcessAllowFailureAsync("docker", $"context inspect {QuoteDockerArgument(context)}");
        return result.ExitCode == 0;
    }

    private async Task<bool> IsDockerResponsiveAsync()
    {
        var result = await RunProcessAllowFailureAsync("docker", "info --format \"{{{{.ServerVersion}}}}\"");
        return result.ExitCode == 0;
    }

    private async Task<bool> IsDockerDaemonAsync(string context, string expectedOs)
    {
        var result = await RunProcessAllowFailureAsync(
            "docker",
            $"--context {context} info --format \"{{{{.OSType}}}}\"");

        return result.ExitCode == 0 &&
               result.StdOut.Contains(expectedOs, StringComparison.OrdinalIgnoreCase);
    }

    private async Task WaitForDockerResponsiveAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now.Add(timeout);
        while (DateTimeOffset.Now < deadline)
        {
            if (await IsDockerResponsiveAsync())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    private async Task<bool> WaitForDockerDaemonAsync(string context, string expectedOs, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now.Add(timeout);
        while (DateTimeOffset.Now < deadline)
        {
            if (await IsDockerDaemonAsync(context, expectedOs))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        return false;
    }

    private async Task<ProcessExecutionResult> RunProcessAllowFailureAsync(string fileName, string arguments, string? workingDir = null)
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

    private sealed record ProcessExecutionResult(int ExitCode, string StdOut, string StdErr);

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
            string.Equals(Path.GetFullPath(x.FilePath), Path.GetFullPath(projectPath), StringComparison.OrdinalIgnoreCase));
        return ResolveCoverageCollectorArgument(project?.BuildMetadata.CoverageCollector ?? CoverageCollectorType.Unknown);
    }

    private static string ResolveCoverageCollectorArgument(CoverageCollectorType collectorType)
    {
        return collectorType switch
        {
            CoverageCollectorType.Coverlet => "XPlat Code Coverage",
            _ => "Code Coverage;Format=Cobertura"
        };
    }

    private string CurrentDockerContext =>
        string.IsNullOrWhiteSpace(_dockerContextOverride)
            ? _context.Project.Config.RuntimeConfig.Docker.Context
            : _dockerContextOverride;

    private string ResolveDockerContext(bool requiresWindows)
    {
        if (requiresWindows)
        {
            return DockerRuntimePathMapper.WindowsContextName;
        }

        var configuredContext = _context.Project.Config.RuntimeConfig.Docker.Context;
        if (string.IsNullOrWhiteSpace(configuredContext) ||
            _pathMapper.IsWindowsContext(configuredContext))
        {
            return DockerRuntimePathMapper.LinuxContextName;
        }

        return configuredContext;
    }

    private bool SolutionSetRequiresWindows(List<string> solutionFilenames)
    {
        var solutionProjects = GetProjectsForSolutions(solutionFilenames).ToList();
        return solutionProjects.Any(project =>
            project.BuildMetadata.WindowsRequirement is WindowsRequirementType.Required or WindowsRequirementType.LikelyRequired);
    }

    private string ResolveCommonBaselineTestFramework(List<string> solutionFilenames)
    {
        var testProjects = GetProjectsForSolutions(solutionFilenames)
            .Where(project => project.BuildMetadata.IsTestProject)
            .ToList();

        if (testProjects.Count == 0)
        {
            throw new InvalidOperationException(
                $"No test projects were found for baseline solution(s): {string.Join(", ", solutionFilenames)}.");
        }

        var commonFrameworks = new HashSet<string>(
            GetProjectTargetFrameworks(testProjects[0]),
            StringComparer.OrdinalIgnoreCase);

        foreach (var testProject in testProjects.Skip(1))
        {
            commonFrameworks.IntersectWith(GetProjectTargetFrameworks(testProject));
        }

        var selectedFramework = commonFrameworks
            .Select(ParseFrameworkPreference)
            .OrderByDescending(framework => framework.IsModernNet)
            .ThenBy(framework => framework.IsLegacyFramework)
            .ThenByDescending(framework => framework.Major)
            .ThenByDescending(framework => framework.Minor)
            .ThenByDescending(framework => framework.Framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            .Select(framework => framework.Framework)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(selectedFramework))
        {
            var projectTargets = string.Join(
                "; ",
                testProjects.Select(project =>
                    $"{Path.GetFileName(project.FilePath)}=[{string.Join(",", GetProjectTargetFrameworks(project))}]"));
            throw new InvalidOperationException(
                $"No common target framework exists across baseline test projects for solution(s) {string.Join(", ", solutionFilenames)}. {projectTargets}");
        }

        return selectedFramework;
    }

    private IEnumerable<CSharpProjectModel> GetProjectsForSolutions(List<string> solutionFilenames)
    {
        var selectedSolutions = _context.Project.Solutions
            .Where(solution => solutionFilenames.Contains(Path.GetFileName(solution.FilePath), StringComparer.OrdinalIgnoreCase))
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
        var candidates = GetProjectTargetFrameworks(project);

        return candidates
            .Select(ParseFrameworkPreference)
            .OrderByDescending(x => x.IsModernNet)
            .ThenBy(x => x.IsLegacyFramework)
            .ThenByDescending(x => x.Major)
            .ThenByDescending(x => x.Minor)
            .ThenByDescending(x => x.Framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Framework)
            .FirstOrDefault();
    }

    private static FrameworkPreference ParseFrameworkPreference(string framework)
    {
        var legacyMatch = Regex.Match(framework, @"^net(?<major>[1-4])(?<minor>\d)(?<patch>\d)?$", RegexOptions.IgnoreCase);
        if (legacyMatch.Success)
        {
            var legacyMajor = int.TryParse(legacyMatch.Groups["major"].Value, out var parsedLegacyMajor)
                ? parsedLegacyMajor
                : -1;
            var legacyMinor = int.TryParse(legacyMatch.Groups["minor"].Value, out var parsedLegacyMinor)
                ? parsedLegacyMinor
                : 0;
            return new FrameworkPreference(framework, false, true, legacyMajor, legacyMinor);
        }

        var modernMatch = Regex.Match(framework, @"^net(?<major>[5-9]|\d{2,})(?:\.(?<minor>\d+))?$", RegexOptions.IgnoreCase);
        if (modernMatch.Success)
        {
            var modernMajor = int.TryParse(modernMatch.Groups["major"].Value, out var parsedModernMajor)
                ? parsedModernMajor
                : -1;
            var modernMinor = int.TryParse(modernMatch.Groups["minor"].Value, out var parsedModernMinor)
                ? parsedModernMinor
                : 0;
            return new FrameworkPreference(framework, true, false, modernMajor, modernMinor);
        }

        var netStandardOrCoreMatch = Regex.Match(framework, @"^net(?:standard|coreapp)(?<major>\d+)(?:\.(?<minor>\d+))?$", RegexOptions.IgnoreCase);
        if (netStandardOrCoreMatch.Success)
        {
            var major = int.TryParse(netStandardOrCoreMatch.Groups["major"].Value, out var parsedMajor)
                ? parsedMajor
                : -1;
            var minor = int.TryParse(netStandardOrCoreMatch.Groups["minor"].Value, out var parsedMinor)
                ? parsedMinor
                : 0;
            return new FrameworkPreference(framework, false, false, major, minor);
        }

        if (!framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return new FrameworkPreference(framework, false, false, -1, -1);
        }

        return new FrameworkPreference(framework, false, false, -1, -1);
    }

    private sealed class ProcessExecutionException : Exception
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
            return $"Command: {FileName} {Arguments}{Environment.NewLine}ExitCode: {ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{StdOut}{Environment.NewLine}STDERR:{Environment.NewLine}{StdErr}";
        }
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

    private sealed record FrameworkPreference(string Framework, bool IsModernNet, bool IsLegacyFramework, int Major, int Minor);
}

public sealed record DockerCompilationValidationResult(bool IsSuccess, string? LogText, string? StructuredErrors)
{
    public static DockerCompilationValidationResult Success(string? logText) => new(true, logText, null);

    public static DockerCompilationValidationResult Failure(string logText, string? structuredErrors = null) =>
        new(false, logText, structuredErrors);
}
