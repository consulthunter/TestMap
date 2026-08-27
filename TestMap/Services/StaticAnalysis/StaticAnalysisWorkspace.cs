using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace TestMap.Services.StaticAnalysis;

public sealed class StaticAnalysisWorkspace : IStaticAnalysisWorkspace, IDisposable
{
    private MSBuildWorkspace _workspace;
    private readonly Dictionary<string, Task<Solution>> _solutionsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<Project>> _projectsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _workspaceFailures = new();

    // Paths restore has already been attempted for. Restore writes to disk, so the outcome
    // survives ResetWorkspace and must not be retried when the in-memory workspace is rebuilt.
    private readonly HashSet<string> _restoreAttempted = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan RestoreTimeout = TimeSpan.FromMinutes(5);

    public StaticAnalysisWorkspace()
    {
        _workspace = CreateWorkspace();
    }

    public IReadOnlyList<string> WorkspaceFailures => _workspaceFailures;

    public void ClearWorkspaceFailures()
    {
        _workspaceFailures.Clear();
    }

    public Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(solutionPath);
        if (!_solutionsByPath.TryGetValue(normalizedPath, out var solutionTask))
        {
            solutionTask = OpenSanitizedSolutionAsync(normalizedPath, cancellationToken);
            _solutionsByPath[normalizedPath] = solutionTask;
        }

        return solutionTask;
    }

    public async Task<Project> OpenProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var normalizedProjectPath = NormalizePath(projectPath);

        foreach (var solutionTask in _solutionsByPath.Values)
        {
            var cachedSolution = await solutionTask;
            var cachedProject = cachedSolution.Projects.FirstOrDefault(project =>
                string.Equals(project.FilePath, normalizedProjectPath, StringComparison.OrdinalIgnoreCase));
            if (cachedProject != null) return cachedProject;
        }

        if (!_projectsByPath.TryGetValue(normalizedProjectPath, out var projectTask))
        {
            projectTask = OpenSanitizedProjectAsync(normalizedProjectPath, cancellationToken);
            _projectsByPath[normalizedProjectPath] = projectTask;
        }

        return await projectTask;
    }

    public async Task<Project> RefreshProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ResetWorkspace();
        var normalizedProjectPath = NormalizePath(projectPath);
        var projectTask = OpenSanitizedProjectAsync(normalizedProjectPath, cancellationToken);
        _projectsByPath[normalizedProjectPath] = projectTask;
        return await projectTask;
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private MSBuildWorkspace CreateWorkspace()
    {
        var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            _workspaceFailures.Add($"{args.Diagnostic.Kind}: {args.Diagnostic.Message}");
        });

        return workspace;
    }

    private void ResetWorkspace()
    {
        _workspace.Dispose();
        _workspace = CreateWorkspace();
        _solutionsByPath.Clear();
        _projectsByPath.Clear();
        _workspaceFailures.Clear();
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private async Task<Solution> OpenSanitizedSolutionAsync(string solutionPath, CancellationToken cancellationToken)
    {
        await EnsureRestoredAsync(solutionPath, cancellationToken);
        var solution = await _workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
        return RemoveAnalyzerReferences(solution);
    }

    private async Task<Project> OpenSanitizedProjectAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        await EnsureRestoredAsync(projectPath, cancellationToken);
        var project = await _workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        var solution = RemoveAnalyzerReferences(project.Solution);
        return solution.GetProject(project.Id) ?? project;
    }

    /// <summary>
    /// Restores NuGet packages before the path is first opened. A PackageReference resolves to a
    /// metadata reference only through obj/project.assets.json, so an unrestored tree yields a
    /// compilation carrying the targeting pack alone: project references still bind, but every
    /// package type -- Assert above all -- is an unknown identifier. Analysis that reads symbols
    /// silently degrades to syntax against such a compilation, so restore runs once per path,
    /// before the workspace snapshot everything else is served from.
    ///
    /// Restore is enrichment, never a gate. A repository that cannot restore -- no network, a
    /// private feed, an unsupported SDK -- is recorded as a workspace failure and opened anyway,
    /// which is exactly the behaviour that existed before this ran at all.
    /// </summary>
    private async Task EnsureRestoredAsync(string path, CancellationToken cancellationToken)
    {
        if (!_restoreAttempted.Add(path)) return;
        if (!File.Exists(path)) return;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"restore \"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory
            };

            // MSBuildLocator points these at the Roslyn host's MSBuild. A child dotnet that
            // inherits them loads that MSBuild instead of its own and fails to resolve the NuGet
            // SDK (MSB4237), so the child must discover its own toolchain.
            startInfo.Environment.Remove("MSBUILD_EXE_PATH");
            startInfo.Environment.Remove("MSBuildExtensionsPath");
            startInfo.Environment.Remove("MSBuildSDKsPath");

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _workspaceFailures.Add($"Restore skipped: could not start dotnet for '{path}'.");
                return;
            }

            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RestoreTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                _workspaceFailures.Add(
                    $"Restore timed out after {RestoreTimeout.TotalMinutes:0} minutes for '{path}'.");
                return;
            }

            if (process.ExitCode != 0)
            {
                var detail = (await stderr).Trim();
                if (detail.Length == 0) detail = (await stdout).Trim();
                _workspaceFailures.Add(
                    $"Restore failed for '{path}' (exit {process.ExitCode}): {Truncate(detail)}");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _workspaceFailures.Add($"Restore failed for '{path}': {exception.Message}");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(true);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException
                or System.ComponentModel.Win32Exception)
        {
            // The process already exited or cannot be killed; nothing further to do.
        }
    }

    private static string Truncate(string value)
    {
        return value.Length <= 500 ? value : value[..500] + "...";
    }

    private static Solution RemoveAnalyzerReferences(Solution solution)
    {
        foreach (var project in solution.Projects)
            if (project.AnalyzerReferences.Any())
                solution = solution.WithProjectAnalyzerReferences(project.Id, []);

        return solution;
    }
}
