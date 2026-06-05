using System.Diagnostics;
using System.Text;
using TestMap.Models.AgentTools;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Runtime;

namespace TestMap.Services.AgentTools;

/// <summary>
/// Production Docker-based runner for agentic tool experiments.
/// </summary>
public sealed class DockerToolRunner : IAgentToolRunner
{
    private readonly RuntimeConfig _runtimeConfig;

    public DockerToolRunner(
        RuntimeConfig runtimeConfig,
        IAgentToolEnvironmentResolver envResolver)
    {
        _runtimeConfig = runtimeConfig;
    }

    public Task<ToolAvailabilityResult> CheckAvailabilityAsync(
        ExperimentToolConfig tool,
        CancellationToken cancellationToken)
    {
        return CheckAvailabilityCoreAsync(tool, cancellationToken);
    }

    public Task<ToolRunPreparationResult> PrepareAsync(
        ToolRunRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspacePath) || !Directory.Exists(request.WorkspacePath))
            return Task.FromResult(new ToolRunPreparationResult
            {
                Success = false,
                ErrorMessage = $"Workspace path does not exist: '{request.WorkspacePath}'."
            });

        if (string.IsNullOrWhiteSpace(request.ArtifactPath))
            return Task.FromResult(new ToolRunPreparationResult
            {
                Success = false,
                ErrorMessage = "Artifact path is required."
            });

        Directory.CreateDirectory(request.ArtifactPath);
        return Task.FromResult(new ToolRunPreparationResult { Success = true });
    }

    public Task<ToolRunResult> RunAsync(
        ToolRunRequest request,
        CancellationToken cancellationToken)
    {
        return RunCoreAsync(request, cancellationToken);
    }

    public Task<ToolRunCollectionResult> CollectAsync(
        ToolRunRequest request,
        CancellationToken cancellationToken)
    {
        var patchPath = Path.Combine(request.ArtifactPath, "patch.diff");
        var changedFilesPath = Path.Combine(request.ArtifactPath, "changed-files.txt");
        var beforePath = Path.Combine(request.ArtifactPath, "git-before.txt");
        var afterPath = Path.Combine(request.ArtifactPath, "git-after.txt");

        var changedFiles = File.Exists(changedFilesPath)
            ? File.ReadAllLines(changedFilesPath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList()
            : [];

        return Task.FromResult(new ToolRunCollectionResult
        {
            PatchDiff = File.Exists(patchPath) ? File.ReadAllText(patchPath) : string.Empty,
            ChangedFiles = changedFiles,
            GitStatusBefore = File.Exists(beforePath) ? File.ReadAllText(beforePath) : string.Empty,
            GitStatusAfter = File.Exists(afterPath) ? File.ReadAllText(afterPath) : string.Empty
        });
    }

    /// <summary>
    /// Resolves the Docker image name for a tool from the runtime config registry.
    /// Uses <see cref="ExperimentToolConfig.ImageKey"/> when set; falls back to
    /// <see cref="ExperimentToolConfig.Id"/>. Public for unit testing without a container run.
    /// </summary>
    public string ResolveImageName(ExperimentToolConfig tool)
    {
        var key = tool.ImageKey ?? tool.Id;
        if (!_runtimeConfig.Docker.Images.AgentTools.TryGetValue(key, out var imageName))
            throw new InvalidOperationException(
                $"No Docker image registered for agent tool key '{key}'. " +
                $"Available keys: {string.Join(", ", _runtimeConfig.Docker.Images.AgentTools.Keys)}");
        return imageName;
    }

    private async Task<ToolAvailabilityResult> CheckAvailabilityCoreAsync(
        ExperimentToolConfig tool,
        CancellationToken cancellationToken)
    {
        var imageName = ResolveImageName(tool);
        var missingSecrets = tool.RequiredEnvironmentVariables
            .Where(x => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(x)))
            .ToList();

        if (missingSecrets.Count > 0)
        {
            return new ToolAvailabilityResult
            {
                ToolId = tool.Id,
                IsAvailable = false,
                ImageName = imageName,
                MissingSecrets = missingSecrets,
                UnavailableReason = $"Missing required environment variable(s): {string.Join(", ", missingSecrets)}"
            };
        }

        var result = await RunProcessAsync(
            "docker",
            BuildDockerPrefix(["image", "inspect", imageName]),
            null,
            TimeSpan.FromSeconds(30),
            cancellationToken);

        return new ToolAvailabilityResult
        {
            ToolId = tool.Id,
            IsAvailable = result.ExitCode == 0,
            ImageName = imageName,
            UnavailableReason = result.ExitCode == 0
                ? null
                : $"Docker image inspect failed for '{imageName}': {TrimForMessage(result.StdErr, result.StdOut)}"
        };
    }

    private async Task<ToolRunResult> RunCoreAsync(
        ToolRunRequest request,
        CancellationToken cancellationToken)
    {
        var preparation = await PrepareAsync(request, cancellationToken);
        if (!preparation.Success)
            throw new InvalidOperationException(preparation.ErrorMessage);

        var imageName = ResolveImageName(request.ToolConfig);
        var timeout = request.Attempt.TimeoutSeconds > 0
            ? TimeSpan.FromSeconds(request.Attempt.TimeoutSeconds)
            : TimeSpan.FromMinutes(Math.Max(1, request.ToolConfig.TimeoutMinutes));

        var args = new List<string>
        {
            "run",
            "--rm",
            "--name",
            CreateContainerName(request),
            "-v",
            $"{Path.GetFullPath(request.WorkspacePath)}:/workspace",
            "-v",
            $"{Path.GetFullPath(request.ArtifactPath)}:/attempt"
        };

        foreach (var (name, value) in BuildContainerEnvironment(request))
        {
            args.Add("-e");
            args.Add($"{name}={value}");
        }

        args.Add(imageName);

        var result = await RunProcessAsync(
            "docker",
            BuildDockerPrefix(args),
            request.WorkspacePath,
            timeout,
            cancellationToken);

        var versionPath = Path.Combine(request.ArtifactPath, $"{request.ToolConfig.Id}-version.txt");
        return new ToolRunResult
        {
            ExitCode = result.TimedOut ? 124 : result.ExitCode,
            StdOut = result.StdOut,
            StdErr = result.StdErr,
            Elapsed = result.Elapsed,
            TimedOut = result.TimedOut,
            ToolVersion = File.Exists(versionPath) ? File.ReadAllText(versionPath).Trim() : string.Empty
        };
    }

    private IReadOnlyList<string> BuildDockerPrefix(IReadOnlyList<string> args)
    {
        if (string.IsNullOrWhiteSpace(_runtimeConfig.Docker.DefaultContext))
            return args;

        return ["--context", _runtimeConfig.Docker.DefaultContext, .. args];
    }

    /// <summary>
    /// Builds the container environment from TestMap's normalized provider tuple and tool-specific
    /// non-secret overrides.
    /// </summary>
    public static Dictionary<string, string> BuildContainerEnvironment(ToolRunRequest request)
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.ResolvedEnvironment)
            env[item.Key] = item.Value;

        ApplyProviderApiKeyAliases(env);
        ApplyToolProviderEnvironment(request.ToolConfig, env);

        if (env.TryGetValue("TESTMAP_LLM_MODEL", out var model) &&
            !env.ContainsKey("CODEX_MODEL"))
            env["CODEX_MODEL"] = model;

        if (env.TryGetValue("TESTMAP_LLM_BASE_URL", out var baseUrl) &&
            !env.ContainsKey("OPENAI_BASE_URL"))
            env["OPENAI_BASE_URL"] = baseUrl;

        foreach (var item in request.ToolConfig.Environment)
            env[item.Key] = item.Value;

        return env;
    }

    private static void ApplyProviderApiKeyAliases(Dictionary<string, string> env)
    {
        if (!env.TryGetValue("TESTMAP_LLM_API_KEY", out var apiKey) || string.IsNullOrEmpty(apiKey))
            return;

        var provider = ResolveProviderId(env);
        switch (provider)
        {
            case "anthropic":
                AddIfMissing(env, "ANTHROPIC_API_KEY", apiKey);
                break;
            case "google-gemini":
            case "google-cloud":
            case "gemini":
                AddIfMissing(env, "GEMINI_API_KEY", apiKey);
                AddIfMissing(env, "GOOGLE_API_KEY", apiKey);
                break;
            default:
                AddIfMissing(env, "OPENAI_API_KEY", apiKey);
                break;
        }
    }

    private static void ApplyToolProviderEnvironment(
        ExperimentToolConfig tool,
        Dictionary<string, string> env)
    {
        var toolId = tool.Id;
        var provider = ResolveProviderId(env);
        var rawModel = env.TryGetValue("TESTMAP_LLM_MODEL", out var model) ? model : string.Empty;
        var providerModel = NormalizeProviderModel(provider, rawModel);

        if (toolId.Equals("openhands", StringComparison.OrdinalIgnoreCase))
        {
            AddIfMissing(env, "LLM_MODEL", providerModel);
            if (env.TryGetValue("TESTMAP_LLM_API_KEY", out var apiKey))
                AddIfMissing(env, "LLM_API_KEY", apiKey);
            if (env.TryGetValue("TESTMAP_LLM_BASE_URL", out var baseUrl))
                AddIfMissing(env, "LLM_BASE_URL", baseUrl);
            return;
        }

        if (toolId.Equals("aider", StringComparison.OrdinalIgnoreCase))
        {
            AddIfMissing(env, "AIDER_MODEL", providerModel);
            return;
        }

        if (toolId.Equals("mini-swe-agent", StringComparison.OrdinalIgnoreCase))
        {
            AddIfMissing(env, "MINI_MODEL", providerModel);
            return;
        }

        if (toolId.Equals("gemini", StringComparison.OrdinalIgnoreCase))
            AddIfMissing(env, "GEMINI_MODEL", rawModel);
    }

    private static string ResolveProviderId(IReadOnlyDictionary<string, string> env)
    {
        return env.TryGetValue("TESTMAP_LLM_PROVIDER", out var provider)
            ? provider.Trim().ToLowerInvariant()
            : string.Empty;
    }

    private static string NormalizeProviderModel(string provider, string model)
    {
        if (string.IsNullOrWhiteSpace(model) || model.Contains('/'))
            return model;

        return provider switch
        {
            "anthropic" => $"anthropic/{model}",
            "google-gemini" or "google-cloud" or "gemini" => $"gemini/{model}",
            "ollama" => $"ollama/{model}",
            "amazon" => $"bedrock/{model}",
            _ => $"openai/{model}"
        };
    }

    private static void AddIfMissing(Dictionary<string, string> env, string key, string value)
    {
        if (!string.IsNullOrEmpty(value) && !env.ContainsKey(key))
            env[key] = value;
    }

    private static string CreateContainerName(ToolRunRequest request)
    {
        var toolId = SanitizeForDockerName(request.ToolConfig.Id);
        var attemptId = request.Attempt.Id == 0 ? Guid.NewGuid().ToString("N")[..8] : request.Attempt.Id.ToString();
        return $"testmap-{toolId}-{attemptId}";
    }

    private static string SanitizeForDockerName(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }

    private static async Task<ProcessExecutionResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.CurrentDirectory
                    : workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            },
            EnableRaisingEvents = true
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None);
        }

        stopwatch.Stop();
        return new ProcessExecutionResult(
            timedOut ? 124 : process.ExitCode,
            stdout.ToString(),
            stderr.ToString(),
            stopwatch.Elapsed,
            timedOut);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cleanup after timeout.
        }
    }

    private static string TrimForMessage(params string[] values)
    {
        var value = string.Join(" ", values.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        return value.Length <= 500 ? value : value[..500];
    }

    private sealed record ProcessExecutionResult(
        int ExitCode,
        string StdOut,
        string StdErr,
        TimeSpan Elapsed,
        bool TimedOut);
}
