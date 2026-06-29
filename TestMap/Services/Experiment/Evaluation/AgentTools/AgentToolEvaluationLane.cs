using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TestMap.App;
using TestMap.Models.AgentTools;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Repositories.AgentTools;
using TestMap.Services.AgentTools;
using TestMap.Services.Configuration;
using TestMap.Services.Experiment.TaskCards;

namespace TestMap.Services.Experiment.Evaluation.AgentTools;

/// <summary>
/// Phase 1 stub for the agent tool evaluation lane. Creates work items (empty for now) and
/// orchestrates the prepare→run→collect lifecycle against an <see cref="IAgentToolRunner"/>.
/// Post-attempt measurement is deferred to Phase 2.
/// </summary>
public sealed class AgentToolEvaluationLane : IExperimentEvaluationLane
{
    private readonly IAgentToolRunner _runner;
    private readonly IAgentToolEnvironmentResolver _envResolver;
    private readonly ToolAttemptRepository _attemptRepo;
    private readonly IReadOnlyList<ExperimentToolConfig> _tools;
    private readonly ProjectContext? _projectContext;
    private readonly TestMapConfig? _config;
    private readonly TaskCardWriter? _taskCardWriter;

    public AgentToolEvaluationLane(
        IAgentToolRunner runner,
        IAgentToolEnvironmentResolver envResolver,
        ToolAttemptRepository attemptRepo,
        IReadOnlyList<ExperimentToolConfig> tools)
    {
        _runner = runner;
        _envResolver = envResolver;
        _attemptRepo = attemptRepo;
        _tools = tools;
    }

    public AgentToolEvaluationLane(
        IAgentToolRunner runner,
        IAgentToolEnvironmentResolver envResolver,
        ToolAttemptRepository attemptRepo,
        IReadOnlyList<ExperimentToolConfig> tools,
        ProjectContext projectContext,
        TestMapConfig config,
        TaskCardWriter taskCardWriter)
        : this(runner, envResolver, attemptRepo, tools)
    {
        _projectContext = projectContext;
        _config = config;
        _taskCardWriter = taskCardWriter;
    }

    public string LaneId => "agent-tools";

    public Task<IReadOnlyList<ExperimentMatrixWorkItem>> CreateWorkItemsAsync(
        ExperimentEvaluationPlanningContext context,
        CancellationToken cancellationToken)
    {
        // One work item per candidate × tool pair.
        // Stubbed: returns empty list until orchestration is wired up in Phase 2.
        IReadOnlyList<ExperimentMatrixWorkItem> items = [];
        return Task.FromResult(items);
    }

    public async Task<ExperimentEvaluationAttemptResult> ExecuteAsync(
        ExperimentEvaluationWorkItemContext context,
        CancellationToken cancellationToken)
    {
        var tool = ResolveTool(context.WorkItem)
                   ?? throw new InvalidOperationException("No tools configured for AgentToolEvaluationLane.");
        var environment = _config == null
            ? new ToolEnvironmentResolution()
            : _envResolver.Resolve(tool, _config.AiProviderConfig, _config.TestingConfig.GenerationConfig);

        if (!environment.IsValid)
            throw new InvalidOperationException(
                $"Tool '{tool.Id}' is missing required secret(s): {string.Join(", ", environment.MissingRequiredSecrets)}");

        var attempt = new ToolAttempt
        {
            ExperimentRunId = context.WorkItem.ExperimentRunId,
            MatrixWorkItemId = context.WorkItem.Id,
            CandidateMethodId = context.Candidate.Id,
            TargetedBaselineId = context.TargetedBaselineId,
            ToolId = tool.Id,
            ImageKey = tool.ImageKey ?? tool.Id,
            RunStatus = ToolRunStatus.Planned,
            StartedAt = DateTime.UtcNow,
            TimeoutSeconds = tool.TimeoutMinutes * 60,
            Model = environment.PersistableMetadata.TryGetValue("model", out var model) ? model : tool.Model ?? string.Empty,
            ProviderId = environment.PersistableMetadata.TryGetValue("provider_id", out var provider) ? provider : string.Empty
        };

        attempt.Id = await _attemptRepo.InsertAsync(attempt, cancellationToken);
        attempt.WorkspacePath = ResolveWorkspacePath();
        attempt.ArtifactPath = ResolveArtifactPath(attempt);
        var logEnvironment = new Dictionary<string, string>(
            environment.NormalizedVars,
            StringComparer.OrdinalIgnoreCase);
        foreach (var item in tool.Environment)
            logEnvironment[item.Key] = item.Value;
        var logPaths = AgentToolLogPathResolver.Resolve(
            attempt.ArtifactPath,
            tool.Id,
            logEnvironment);
        attempt.StdOutLogPath = logPaths.StdOutLogPath;
        attempt.StdErrLogPath = logPaths.StdErrLogPath;
        attempt.JsonlLogPath = logPaths.JsonlLogPath;
        var taskCardContent = CreateTaskCardContent(context, attempt.WorkspacePath);

        if (_taskCardWriter != null && !string.IsNullOrWhiteSpace(attempt.WorkspacePath))
            await _taskCardWriter.WriteAsync(
                attempt.WorkspacePath,
                taskCardContent,
                cancellationToken);
        await WriteAttemptArtifactsAsync(attempt.ArtifactPath, taskCardContent, cancellationToken);

        var request = new ToolRunRequest
        {
            Attempt = attempt,
            ToolConfig = tool,
            WorkspacePath = attempt.WorkspacePath,
            ArtifactPath = attempt.ArtifactPath,
            ResolvedEnvironment = environment.NormalizedVars
        };

        try
        {
            await _attemptRepo.UpdateStatusAsync(attempt.Id, ToolRunStatus.Running, cancellationToken);
            var preparation = await _runner.PrepareAsync(request, cancellationToken);
            if (!preparation.Success)
                throw new InvalidOperationException(preparation.ErrorMessage);

            var result = await _runner.RunAsync(request, cancellationToken);

            attempt.ExitCode = result.ExitCode;
            attempt.ElapsedSeconds = result.Elapsed.TotalSeconds;
            attempt.ToolVersion = result.ToolVersion ?? string.Empty;
            attempt.CompletedAt = DateTime.UtcNow;
            attempt.JsonlLogAvailable = HasJsonlEventLog(attempt.JsonlLogPath);
            attempt.EstimatedPromptTokens = EstimatePromptTokens(taskCardContent.Prompt);
            var usage = ExtractUsage(attempt.ArtifactPath, tool.Id);
            if (usage != null)
            {
                attempt.UsageAvailable = true;
                attempt.UsageSource = usage.Source;
                attempt.InputTokens = usage.InputTokens;
                attempt.OutputTokens = usage.OutputTokens;
            }

            IReadOnlyList<string> collectedChangedFiles = [];
            if (result.TimedOut)
            {
                attempt.RunStatus = ToolRunStatus.TimedOut;
            }
            else
            {
                var collected = await _runner.CollectAsync(request, cancellationToken);
                var collection = new ToolRunCollectionResult
                {
                    PatchDiff = collected.PatchDiff,
                    ChangedFiles = AgentToolChangedPathFilter.Filter(collected.ChangedFiles),
                    GitStatusBefore = collected.GitStatusBefore,
                    GitStatusAfter = collected.GitStatusAfter
                };
                ApplyCollection(attempt, collection);
                attempt.RunStatus = result.ExitCode == 0
                    ? collection.ChangedFiles.Count == 0
                        ? ToolRunStatus.CompletedNoChange
                        : ToolRunStatus.Completed
                    : ToolRunStatus.ToolCrashed;
                collectedChangedFiles = collection.ChangedFiles;
            }

            ApplyOutcomeClassification(attempt);
            if (attempt.RunStatus is ToolRunStatus.TimedOut or ToolRunStatus.ToolCrashed)
                attempt.Notes = BuildToolFailureSummary(attempt, result);
            await _attemptRepo.UpdateAsync(attempt, cancellationToken);

            return new ExperimentEvaluationAttemptResult
            {
                Success = attempt.RunStatus is ToolRunStatus.Completed or ToolRunStatus.CompletedNoChange,
                ToolAttempt = attempt,
                ChangedFiles = collectedChangedFiles,
                ErrorMessage = attempt.RunStatus is ToolRunStatus.Completed or ToolRunStatus.CompletedNoChange
                    ? null
                    : BuildToolFailureSummary(attempt, result)
            };
        }
        catch (Exception ex)
        {
            attempt.RunStatus = ToolRunStatus.ToolCrashed;
            attempt.ValidationOutcome = ToolValidationOutcome.ToolFailed;
            attempt.ObservedOutcome = ToolObservedOutcome.ToolFailed;
            attempt.Notes = ex.Message;
            attempt.CompletedAt = DateTime.UtcNow;
            await _attemptRepo.UpdateAsync(attempt, cancellationToken);
            return new ExperimentEvaluationAttemptResult { Success = false, ErrorMessage = ex.Message, ToolAttempt = attempt };
        }
    }

    private ExperimentToolConfig? ResolveTool(ExperimentMatrixWorkItem workItem)
    {
        if (_tools.Count == 0) return null;

        return _tools.FirstOrDefault(x =>
                   string.Equals(x.Id, workItem.AblationVariantId, StringComparison.OrdinalIgnoreCase))
               ?? _tools.First();
    }

    private string ResolveWorkspacePath()
    {
        return _projectContext?.Project.DirectoryPath ?? string.Empty;
    }

    private string ResolveArtifactPath(ToolAttempt attempt)
    {
        var outputRoot = _projectContext?.Project.OutputPath
                         ?? _config?.RuntimeConfig.FilePaths.OutputDirPath
                         ?? Path.Combine(Environment.CurrentDirectory, "Output");

        return Path.Combine(
            outputRoot,
            "agent-tool-attempts",
            attempt.ExperimentRunId.ToString(),
            attempt.CandidateMethodId.ToString(),
            attempt.ToolId,
            attempt.Id.ToString());
    }

    private static TaskCardContent CreateTaskCardContent(
        ExperimentEvaluationWorkItemContext context,
        string? workspacePath = null)
    {
        var methodContext = context.MethodContext;
        var method = context.Candidate;
        var targetName = methodContext?.Method.MethodName ?? method.MethodName;
        var signature = methodContext?.MethodSignature ?? method.Signature;
        var mappedTests = new[]
            {
                methodContext?.Method.ExistingTestMethodName,
                methodContext?.TestLocation?.TestFilePath
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct()
            .ToList();

        var card = new TaskCard
        {
            Objective = context.WorkItem.Objective.ToString(),
            TargetMemberName = targetName,
            TargetMemberSignature = signature,
            TargetMemberWeakness = methodContext?.CoverageGapSummary ?? string.Empty,
            AccessStrategy = methodContext?.AccessPathSummary ?? string.Empty,
            MappedTests = mappedTests
        };

        return new TaskCardContent
        {
            Card = card,
            Prompt = BuildPrompt(methodContext, method, workspacePath),
            EvidenceSummary = BuildEvidenceSummary(methodContext)
        };
    }

    private static async Task WriteAttemptArtifactsAsync(
        string artifactPath,
        TaskCardContent content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(artifactPath);
        var options = ConfigJsonSerializer.CreateOptions();
        options.WriteIndented = true;

        await File.WriteAllTextAsync(
            Path.Combine(artifactPath, "task-card.json"),
            JsonSerializer.Serialize(content.Card, options),
            cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(artifactPath, "prompt.md"), content.Prompt, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(artifactPath, "evidence-summary.md"),
            content.EvidenceSummary,
            cancellationToken);
    }

    private static void ApplyCollection(ToolAttempt attempt, ToolRunCollectionResult collection)
    {
        attempt.ChangedFilesCount = collection.ChangedFiles.Count;
        attempt.ProductionFilesChanged = collection.ChangedFiles.Count(IsProductionFile);
        attempt.TestFilesChanged = collection.ChangedFiles.Count(IsTestFile);
        attempt.ProjectFilesChanged = collection.ChangedFiles.Count(IsProjectFile);
        attempt.DeletedFilesCount =
            AgentToolChangedPathFilter.CountIncludedDeletedFiles(collection.GitStatusAfter);
    }

    private static void ApplyOutcomeClassification(ToolAttempt attempt)
    {
        switch (attempt.RunStatus)
        {
            case ToolRunStatus.Completed:
                attempt.ValidationOutcome = ToolValidationOutcome.NotEvaluated;
                attempt.ObservedOutcome = ToolObservedOutcome.ChangedNotValidated;
                break;
            case ToolRunStatus.CompletedNoChange:
                attempt.ValidationOutcome = ToolValidationOutcome.NotEvaluated;
                attempt.ObservedOutcome = ToolObservedOutcome.NoChange;
                break;
            case ToolRunStatus.TimedOut:
                attempt.ValidationOutcome = ToolValidationOutcome.TimedOut;
                attempt.ObservedOutcome = ToolObservedOutcome.TimedOut;
                break;
            case ToolRunStatus.Skipped:
                attempt.ValidationOutcome = ToolValidationOutcome.Skipped;
                attempt.ObservedOutcome = ToolObservedOutcome.Skipped;
                break;
            case ToolRunStatus.ToolCrashed:
                attempt.ValidationOutcome = ToolValidationOutcome.ToolFailed;
                attempt.ObservedOutcome = ToolObservedOutcome.ToolFailed;
                break;
            default:
                attempt.ValidationOutcome = ToolValidationOutcome.NotEvaluated;
                attempt.ObservedOutcome = ToolObservedOutcome.NotEvaluated;
                break;
        }
    }

    private static bool IsTestFile(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("test", StringComparison.OrdinalIgnoreCase) &&
               normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProductionFile(string path)
    {
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !IsTestFile(path);
    }

    private static bool IsProjectFile(string path)
    {
        return path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase);
    }

    private static int EstimatePromptTokens(string prompt)
    {
        return string.IsNullOrWhiteSpace(prompt)
            ? 0
            : Math.Max(1, (int)Math.Ceiling(prompt.Length / 4.0));
    }

    internal static ToolUsageSummary? ExtractUsage(string artifactPath, string toolId)
    {
        if (string.IsNullOrWhiteSpace(artifactPath) || !Directory.Exists(artifactPath))
            return null;

        var preferred = Path.Combine(artifactPath, $"{toolId}.events.jsonl");
        var files = File.Exists(preferred)
            ? [preferred]
            : Directory.GetFiles(artifactPath, "*.events.jsonl")
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        ToolUsageSummary? latest = null;
        foreach (var file in files)
        foreach (var line in File.ReadLines(file))
        {
            var parsed = TryParseUsageLine(line, Path.GetFileName(file));
            if (parsed != null)
                latest = parsed;
        }

        return latest
               ?? ExtractOpenHandsPersistedUsage(artifactPath)
               ?? ExtractMiniSweTrajectoryUsage(artifactPath, toolId)
               ?? ExtractCopilotUsage(artifactPath, toolId)
               ?? ExtractAiderStdoutUsage(artifactPath, toolId);
    }

    private static ToolUsageSummary? ExtractOpenHandsPersistedUsage(string artifactPath)
    {
        ToolUsageSummary? latest = null;
        foreach (var file in Directory.EnumerateFiles(artifactPath, "base_state.json", SearchOption.AllDirectories)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var source = Path.GetRelativePath(artifactPath, file);
                var parsed = TryParseOpenHandsPersistedUsage(doc.RootElement, source);
                if (parsed != null)
                    latest = parsed;
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        return latest;
    }

    internal static ToolUsageSummary? TryParseUsageLine(string line, string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String)
                return null;

            var type = typeElement.GetString() ?? string.Empty;
            return type switch
            {
                "turn.completed" when TryGetObject(root, "usage", out var usage) =>
                    TryParseCodexUsage(usage, sourceFile),
                "result" when TryGetObject(root, "usage", out var usage) =>
                    TryParseClaudeUsage(usage, sourceFile),
                "result" when TryGetObject(root, "stats", out var stats) =>
                    TryParseGeminiUsage(stats, sourceFile),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ToolUsageSummary? TryParseCodexUsage(JsonElement usage, string sourceFile)
    {
        var inputTokens = ReadInt(usage, "input_tokens");
        var outputTokens = ReadInt(usage, "output_tokens");
        if (!inputTokens.HasValue && !outputTokens.HasValue)
            return null;

        return new ToolUsageSummary(
            inputTokens,
            outputTokens,
            $"{sourceFile}:turn.completed");
    }

    private static ToolUsageSummary? TryParseClaudeUsage(JsonElement usage, string sourceFile)
    {
        var inputTokens = Sum(
            ReadInt(usage, "input_tokens"),
            ReadInt(usage, "cache_creation_input_tokens"),
            ReadInt(usage, "cache_read_input_tokens"));
        var outputTokens = ReadInt(usage, "output_tokens");
        if (!inputTokens.HasValue && !outputTokens.HasValue)
            return null;

        return new ToolUsageSummary(
            inputTokens,
            outputTokens,
            $"{sourceFile}:result");
    }

    private static ToolUsageSummary? TryParseGeminiUsage(JsonElement stats, string sourceFile)
    {
        var directInput = ReadInt(stats, "input_tokens");
        var directTotal = ReadInt(stats, "total_tokens");
        var directOutput = directTotal.HasValue && directInput.HasValue
            ? Math.Max(0, directTotal.Value - directInput.Value)
            : ReadInt(stats, "output_tokens");
        if (directInput.HasValue || directOutput.HasValue)
        {
            return new ToolUsageSummary(
                directInput,
                directOutput,
                $"{sourceFile}:result.stats");
        }

        if (!TryGetObject(stats, "models", out var models))
            return null;

        long inputTokens = 0;
        long outputTokens = 0;
        var any = false;

        foreach (var model in models.EnumerateObject())
        {
            if (!TryGetObject(model.Value, "tokens", out var tokens))
                continue;

            var prompt = ReadInt(tokens, "prompt");
            var total = ReadInt(tokens, "total");
            var candidates = ReadInt(tokens, "candidates");
            var thoughts = ReadInt(tokens, "thoughts");
            var tool = ReadInt(tokens, "tool");

            if (prompt.HasValue)
            {
                inputTokens += prompt.Value;
                any = true;
            }

            var output = total.HasValue && prompt.HasValue
                ? Math.Max(0, total.Value - prompt.Value)
                : Sum(candidates, thoughts, tool);
            if (output.HasValue)
            {
                outputTokens += output.Value;
                any = true;
            }
        }

        if (!any) return null;

        return new ToolUsageSummary(
            ClampToInt(inputTokens),
            ClampToInt(outputTokens),
            $"{sourceFile}:result.stats.models");
    }

    private static ToolUsageSummary? TryParseOpenHandsPersistedUsage(JsonElement root, string sourceFile)
    {
        if (TryFindObjectProperty(root, "usage_to_metrics", out var usageToMetrics))
            return TryParseOpenHandsUsageToMetrics(usageToMetrics, sourceFile);

        if (TryFindObjectProperty(root, "accumulated_token_usage", out var usage))
            return TryParseOpenHandsTokenUsage(usage, sourceFile);

        return null;
    }

    private static ToolUsageSummary? TryParseOpenHandsUsageToMetrics(JsonElement usageToMetrics, string sourceFile)
    {
        long inputTokens = 0;
        long outputTokens = 0;
        var any = false;

        foreach (var metric in usageToMetrics.EnumerateObject())
        {
            if (!TryGetObject(metric.Value, "accumulated_token_usage", out var usage))
                continue;

            var parsed = TryParseOpenHandsTokenUsage(usage, sourceFile);
            if (parsed == null)
                continue;

            if (parsed.InputTokens.HasValue)
            {
                inputTokens += parsed.InputTokens.Value;
                any = true;
            }

            if (parsed.OutputTokens.HasValue)
            {
                outputTokens += parsed.OutputTokens.Value;
                any = true;
            }
        }

        if (!any) return null;

        return new ToolUsageSummary(
            ClampToInt(inputTokens),
            ClampToInt(outputTokens),
            $"{sourceFile}:stats.usage_to_metrics");
    }

    private static ToolUsageSummary? TryParseOpenHandsTokenUsage(JsonElement usage, string sourceFile)
    {
        var inputTokens = Sum(
            ReadInt(usage, "prompt_tokens"),
            ReadInt(usage, "input_tokens"),
            ReadInt(usage, "cache_read_tokens"),
            ReadInt(usage, "cache_write_tokens"));
        var outputTokens = Sum(
            ReadInt(usage, "completion_tokens"),
            ReadInt(usage, "output_tokens"),
            ReadInt(usage, "reasoning_tokens"));
        if (!inputTokens.HasValue && !outputTokens.HasValue)
            return null;

        return new ToolUsageSummary(
            inputTokens,
            outputTokens,
            $"{sourceFile}:accumulated_token_usage");
    }

    private static ToolUsageSummary? ExtractMiniSweTrajectoryUsage(string artifactPath, string toolId)
    {
        if (!toolId.Equals("mini-swe-agent", StringComparison.OrdinalIgnoreCase))
            return null;

        var trajectoryPath = Path.Combine(artifactPath, "mini-swe-agent-trajectory.json");
        if (!File.Exists(trajectoryPath))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(trajectoryPath));
            return TryParseMiniSweTrajectoryUsage(
                doc.RootElement,
                Path.GetFileName(trajectoryPath));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static ToolUsageSummary? TryParseMiniSweTrajectoryUsage(JsonElement root, string sourceFile)
    {
        long inputTokens = 0;
        long outputTokens = 0;
        var any = false;

        foreach (var usage in EnumerateNamedObjects(root, "usage"))
        {
            var promptTokens = ReadInt(usage, "prompt_tokens") ?? ReadInt(usage, "input_tokens");
            var completionTokens = ReadInt(usage, "completion_tokens") ?? ReadInt(usage, "output_tokens");

            if (promptTokens.HasValue)
            {
                inputTokens += promptTokens.Value;
                any = true;
            }

            if (completionTokens.HasValue)
            {
                outputTokens += completionTokens.Value;
                any = true;
            }
        }

        if (!any) return null;

        return new ToolUsageSummary(
            ClampToInt(inputTokens),
            ClampToInt(outputTokens),
            $"{sourceFile}:usage");
    }

    private static ToolUsageSummary? ExtractAiderStdoutUsage(string artifactPath, string toolId)
    {
        if (!toolId.Equals("aider", StringComparison.OrdinalIgnoreCase))
            return null;

        var stdoutPath = Path.Combine(artifactPath, "aider.stdout.log");
        if (!File.Exists(stdoutPath))
            return null;

        ToolUsageSummary? latest = null;
        foreach (var line in File.ReadLines(stdoutPath))
        {
            var parsed = TryParseAiderTokenLine(line, Path.GetFileName(stdoutPath));
            if (parsed != null)
                latest = parsed;
        }

        return latest;
    }

    private static ToolUsageSummary? TryParseAiderTokenLine(string line, string sourceFile)
    {
        var match = Regex.Match(
            line,
            @"Tokens:\s*(?<sent>[\d.,]+)\s*(?<sentSuffix>[kKmM]?)\s+sent,\s*(?<received>[\d.,]+)\s*(?<receivedSuffix>[kKmM]?)\s+received",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        var inputTokens = ParseScaledTokenCount(
            match.Groups["sent"].Value,
            match.Groups["sentSuffix"].Value);
        var outputTokens = ParseScaledTokenCount(
            match.Groups["received"].Value,
            match.Groups["receivedSuffix"].Value);
        if (!inputTokens.HasValue && !outputTokens.HasValue)
            return null;

        return new ToolUsageSummary(
            inputTokens,
            outputTokens,
            $"{sourceFile}:tokens-line");
    }

    private static ToolUsageSummary? ExtractCopilotUsage(string artifactPath, string toolId)
    {
        if (!toolId.Equals("copilot", StringComparison.OrdinalIgnoreCase))
            return null;

        return ExtractCopilotOtelUsage(artifactPath)
               ?? ExtractCopilotStderrUsage(artifactPath);
    }

    private static ToolUsageSummary? ExtractCopilotOtelUsage(string artifactPath)
    {
        var otelPath = Path.Combine(artifactPath, "copilot-otel.jsonl");
        if (!File.Exists(otelPath))
            return null;

        ToolUsageSummary? latestMetricUsage = null;
        long spanInputTokens = 0;
        long spanOutputTokens = 0;
        var anySpanUsage = false;

        try
        {
            foreach (var line in File.ReadLines(otelPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                using var doc = JsonDocument.Parse(line);
                var metricUsage = TryParseCopilotOtelMetricUsage(doc.RootElement, Path.GetFileName(otelPath));
                if (metricUsage != null)
                    latestMetricUsage = metricUsage;

                var spanUsage = TryParseCopilotOtelSpanUsage(doc.RootElement, Path.GetFileName(otelPath));
                if (spanUsage == null)
                    continue;

                if (spanUsage.InputTokens.HasValue)
                {
                    spanInputTokens += spanUsage.InputTokens.Value;
                    anySpanUsage = true;
                }

                if (spanUsage.OutputTokens.HasValue)
                {
                    spanOutputTokens += spanUsage.OutputTokens.Value;
                    anySpanUsage = true;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }

        if (latestMetricUsage != null)
            return latestMetricUsage;

        return anySpanUsage
            ? new ToolUsageSummary(
                ClampToInt(spanInputTokens),
                ClampToInt(spanOutputTokens),
                $"{Path.GetFileName(otelPath)}:spans")
            : null;
    }

    private static ToolUsageSummary? TryParseCopilotOtelMetricUsage(JsonElement root, string sourceFile)
    {
        ToolUsageSummary? latest = null;
        foreach (var obj in EnumerateObjects(root))
        {
            if (!TryReadString(obj, "name", out var name) ||
                !name.Equals("gen_ai.client.token.usage", StringComparison.OrdinalIgnoreCase))
                continue;

            var parsed = TryParseCopilotTokenUsageMetric(obj, sourceFile);
            if (parsed != null)
                latest = parsed;
        }

        return latest;
    }

    private static ToolUsageSummary? TryParseCopilotTokenUsageMetric(JsonElement metric, string sourceFile)
    {
        long inputTokens = 0;
        long outputTokens = 0;
        var any = false;

        foreach (var point in EnumerateObjects(metric))
        {
            var count = ReadTokenMetricValue(point);
            if (!count.HasValue || !TryReadTokenType(point, out var tokenType))
                continue;

            if (IsInputTokenType(tokenType))
            {
                inputTokens += count.Value;
                any = true;
            }
            else if (IsOutputTokenType(tokenType))
            {
                outputTokens += count.Value;
                any = true;
            }
        }

        if (!any)
            return null;

        return new ToolUsageSummary(
            ClampToInt(inputTokens),
            ClampToInt(outputTokens),
            $"{sourceFile}:gen_ai.client.token.usage");
    }

    private static ToolUsageSummary? TryParseCopilotOtelSpanUsage(JsonElement root, string sourceFile)
    {
        long inputTokens = 0;
        long outputTokens = 0;
        var any = false;

        foreach (var obj in EnumerateObjects(root))
        {
            if (!TryReadString(obj, "type", out var type) ||
                !type.Equals("span", StringComparison.OrdinalIgnoreCase) ||
                !TryReadString(obj, "name", out var name) ||
                !name.StartsWith("chat ", StringComparison.OrdinalIgnoreCase) ||
                !TryGetObject(obj, "attributes", out var attributes))
                continue;

            // Copilot's gen_ai.usage.input_tokens already includes cached/write input.
            // Cache attributes are detail fields and must not be added on top.
            var input = ReadInt(attributes, "gen_ai.usage.input_tokens") ??
                        ReadInt(attributes, "gen_ai.usage.prompt_tokens") ??
                        Sum(
                            ReadInt(attributes, "gen_ai.usage.cache_read_input_tokens"),
                            ReadInt(attributes, "gen_ai.usage.cache_creation_input_tokens"));
            var output = ReadInt(attributes, "gen_ai.usage.output_tokens") ??
                         ReadInt(attributes, "gen_ai.usage.completion_tokens") ??
                         ReadInt(attributes, "gen_ai.usage.reasoning_output_tokens") ??
                         ReadInt(attributes, "gen_ai.usage.reasoning_tokens");

            if (input.HasValue)
            {
                inputTokens += input.Value;
                any = true;
            }

            if (output.HasValue)
            {
                outputTokens += output.Value;
                any = true;
            }
        }

        if (!any)
            return null;

        return new ToolUsageSummary(
            ClampToInt(inputTokens),
            ClampToInt(outputTokens),
            $"{sourceFile}:span.usage");
    }

    private static ToolUsageSummary? ExtractCopilotStderrUsage(string artifactPath)
    {
        var stderrPath = Path.Combine(artifactPath, "copilot.stderr.log");
        if (!File.Exists(stderrPath))
            return null;

        ToolUsageSummary? latest = null;
        foreach (var line in File.ReadLines(stderrPath))
        {
            var parsed = TryParseCopilotTokenLine(line, Path.GetFileName(stderrPath));
            if (parsed != null)
                latest = parsed;
        }

        return latest;
    }

    private static ToolUsageSummary? TryParseCopilotTokenLine(string line, string sourceFile)
    {
        var match = Regex.Match(
            line,
            @"Tokens\s+.*?(?<input>[\d.,]+)\s*(?<inputSuffix>[kKmM]?).*?(?:\u2022|\|).*?(?<output>[\d.,]+)\s*(?<outputSuffix>[kKmM]?)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        var inputTokens = ParseScaledTokenCount(
            match.Groups["input"].Value,
            match.Groups["inputSuffix"].Value);
        var outputTokens = ParseScaledTokenCount(
            match.Groups["output"].Value,
            match.Groups["outputSuffix"].Value);
        if (!inputTokens.HasValue && !outputTokens.HasValue)
            return null;

        return new ToolUsageSummary(
            inputTokens,
            outputTokens,
            $"{sourceFile}:tokens-line");
    }

    private static long? ReadTokenMetricValue(JsonElement point)
    {
        return ReadLong(point, "sum") ??
               ReadLong(point, "value") ??
               (TryGetObject(point, "value", out var value)
                   ? ReadLong(value, "sum") ??
                     ReadLong(value, "value") ??
                     ReadLong(value, "asDouble") ??
                     ReadLong(value, "asInt") ??
                     ReadLong(value, "doubleValue") ??
                     ReadLong(value, "intValue")
                   : null) ??
               ReadLong(point, "asDouble") ??
               ReadLong(point, "asInt") ??
               ReadLong(point, "doubleValue") ??
               ReadLong(point, "intValue");
    }

    private static bool TryReadTokenType(JsonElement point, out string tokenType)
    {
        foreach (var key in new[] { "gen_ai.token.type", "token.type", "type" })
        {
            if (TryReadAttributeString(point, key, out tokenType))
                return true;
        }

        tokenType = string.Empty;
        return false;
    }

    private static bool IsInputTokenType(string tokenType)
    {
        var normalized = tokenType.Trim().ToLowerInvariant();
        return normalized is "input" or "input_tokens" or "prompt" or "prompt_tokens" or
            "cache" or "cached" or "cached_input" or "cached_input_tokens" or
            "cache_read" or "cache_read_input_tokens" or "cache_write" or "cache_creation_input_tokens";
    }

    private static bool IsOutputTokenType(string tokenType)
    {
        var normalized = tokenType.Trim().ToLowerInvariant();
        return normalized is "output" or "output_tokens" or "completion" or "completion_tokens" or
            "reasoning" or "reasoning_tokens" or "reasoning_output_tokens";
    }

    private static int? ReadCopilotAttributeTokenSum(JsonElement obj, params string[] keys)
    {
        long total = 0;
        var any = false;
        foreach (var key in keys)
        {
            var value = ReadAttributeLong(obj, key);
            if (!value.HasValue)
                continue;

            total += value.Value;
            any = true;
        }

        return any ? ClampToInt(total) : null;
    }

    private static long? ReadAttributeLong(JsonElement obj, string key)
    {
        if (obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty(key, out var direct))
            return ReadLong(direct);

        if (!TryGetAttributeValue(obj, key, out var value))
            return null;

        return ReadLong(value);
    }

    private static bool TryReadAttributeString(JsonElement obj, string key, out string value)
    {
        if (obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty(key, out var direct) &&
            TryReadStringValue(direct, out value))
            return true;

        if (TryGetAttributeValue(obj, key, out var attributeValue) &&
            TryReadStringValue(attributeValue, out value))
            return true;

        value = string.Empty;
        return false;
    }

    private static bool TryGetAttributeValue(JsonElement obj, string key, out JsonElement value)
    {
        if (!obj.TryGetProperty("attributes", out var attributes))
        {
            value = default;
            return false;
        }

        if (attributes.ValueKind == JsonValueKind.Object)
            return attributes.TryGetProperty(key, out value);

        if (attributes.ValueKind == JsonValueKind.Array)
        {
            foreach (var attribute in attributes.EnumerateArray())
            {
                if (attribute.ValueKind != JsonValueKind.Object ||
                    !TryReadString(attribute, "key", out var attributeKey) ||
                    !attributeKey.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                    !attribute.TryGetProperty("value", out value))
                    continue;

                return true;
            }
        }

        value = default;
        return false;
    }

    private static long? ReadLong(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var value))
            return null;

        return ReadLong(value);
    }

    private static long? ReadLong(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.Number when value.TryGetDecimal(out var number) =>
                (long)Math.Round(number, MidpointRounding.AwayFromZero),
            JsonValueKind.String when decimal.TryParse(
                value.GetString(),
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var number) => (long)Math.Round(number, MidpointRounding.AwayFromZero),
            JsonValueKind.Object => ReadLong(value, "sum") ??
                                    ReadLong(value, "intValue") ??
                                    ReadLong(value, "doubleValue") ??
                                    ReadLong(value, "stringValue"),
            _ => null
        };
    }

    private static bool TryReadString(JsonElement obj, string propertyName, out string value)
    {
        if (obj.TryGetProperty(propertyName, out var property) &&
            TryReadStringValue(property, out value))
            return true;

        value = string.Empty;
        return false;
    }

    private static bool TryReadStringValue(JsonElement valueElement, out string value)
    {
        if (valueElement.ValueKind == JsonValueKind.String)
        {
            value = valueElement.GetString() ?? string.Empty;
            return true;
        }

        if (valueElement.ValueKind == JsonValueKind.Object &&
            valueElement.TryGetProperty("stringValue", out var stringValue) &&
            stringValue.ValueKind == JsonValueKind.String)
        {
            value = stringValue.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static int? ParseScaledTokenCount(string value, string suffix)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Replace(",", string.Empty);
        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var parsed))
            return null;

        var scale = suffix.ToLowerInvariant() switch
        {
            "k" => 1_000m,
            "m" => 1_000_000m,
            _ => 1m
        };

        return ClampToInt((long)Math.Round(parsed * scale, MidpointRounding.AwayFromZero));
    }

    private static int? Sum(params int?[] values)
    {
        long total = 0;
        var any = false;
        foreach (var value in values)
        {
            if (!value.HasValue) continue;
            total += value.Value;
            any = true;
        }

        return any ? ClampToInt(total) : null;
    }

    private static int? ReadInt(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => ClampToInt(number),
            JsonValueKind.String when long.TryParse(value.GetString(), out var number) => ClampToInt(number),
            _ => null
        };
    }

    private static bool TryGetObject(JsonElement obj, string propertyName, out JsonElement value)
    {
        if (obj.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Object)
            return true;

        value = default;
        return false;
    }

    private static IEnumerable<JsonElement> EnumerateNamedObjects(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName) &&
                    property.Value.ValueKind == JsonValueKind.Object)
                    yield return property.Value;

                foreach (var match in EnumerateNamedObjects(property.Value, propertyName))
                    yield return match;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            foreach (var match in EnumerateNamedObjects(item, propertyName))
                yield return match;
        }
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            foreach (var match in EnumerateObjects(property.Value))
                yield return match;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            foreach (var match in EnumerateObjects(item))
                yield return match;
        }
    }

    private static bool TryFindObjectProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryGetObject(element, propertyName, out value))
                return true;

            foreach (var property in element.EnumerateObject())
            {
                if (TryFindObjectProperty(property.Value, propertyName, out value))
                    return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindObjectProperty(item, propertyName, out value))
                    return true;
            }
        }

        value = default;
        return false;
    }

    private static int ClampToInt(long value)
    {
        if (value > int.MaxValue) return int.MaxValue;
        if (value < int.MinValue) return int.MinValue;
        return (int)value;
    }

    private static bool HasJsonlEventLog(string jsonlLogPath)
    {
        if (string.IsNullOrWhiteSpace(jsonlLogPath) || !File.Exists(jsonlLogPath))
            return false;

        foreach (var line in File.ReadLines(jsonlLogPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    return true;
            }
            catch (JsonException)
            {
                // A .jsonl suffix is not enough; malformed tool output stays a text log.
            }
        }

        return false;
    }

    private static string BuildToolFailureSummary(ToolAttempt attempt, ToolRunResult result)
    {
        return attempt.RunStatus switch
        {
            ToolRunStatus.TimedOut => $"Tool '{attempt.ToolId}' timed out after {attempt.TimeoutSeconds} seconds.",
            ToolRunStatus.ToolCrashed => $"Tool '{attempt.ToolId}' exited with code {result.ExitCode}.",
            _ => string.Empty
        };
    }

    internal sealed record ToolUsageSummary(
        int? InputTokens,
        int? OutputTokens,
        string Source);

    private static string BuildPrompt(
        TestMap.Services.TestGeneration.TargetSelection.CandidateMethodContext? context,
        CandidateMethod method,
        string? workspacePath = null)
    {
        var targetName = context?.Method.MethodName ?? method.MethodName;
        var signature = context?.MethodSignature ?? method.Signature;
        var builder = new StringBuilder();
        builder.AppendLine("You are running inside a TestMap agentic tool evaluation.");
        builder.AppendLine();
        builder.AppendLine("Task: add or extend tests for the target method. Keep changes focused.");
        builder.AppendLine("Do not remove existing tests. Avoid unrelated production changes.");
        builder.AppendLine();
        builder.AppendLine($"Target method: {targetName}");
        builder.AppendLine($"Signature: {signature}");

        if (context == null)
            return builder.ToString();

        builder.AppendLine($"Containing class: {context.ContainingClass}");
        builder.AppendLine($"Source file: {ToContainerWorkspacePath(context.SourceFilePath, workspacePath)}");
        builder.AppendLine($"Source project: {ToContainerWorkspacePath(context.SourceProjectPath, workspacePath)}");
        builder.AppendLine($"Test project: {ToContainerWorkspacePath(context.TestProjectPath, workspacePath)}");
        builder.AppendLine($"Test file: {ToContainerWorkspacePath(context.TestFilePath, workspacePath)}");
        builder.AppendLine($"Test framework: {context.TestFramework}");
        builder.AppendLine();
        builder.AppendLine("Coverage gap:");
        builder.AppendLine(context.CoverageGapSummary);
        builder.AppendLine();
        builder.AppendLine("Mutation summary:");
        builder.AppendLine(context.MutationSummary);
        builder.AppendLine();
        builder.AppendLine("Relevant test support context:");
        builder.AppendLine(context.TestSupportContext);
        builder.AppendLine();
        builder.AppendLine("Example or mapped test:");
        builder.AppendLine(context.ExampleTest);

        return builder.ToString();
    }

    private static string ToContainerWorkspacePath(string? path, string? workspacePath)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(workspacePath))
            return path ?? string.Empty;

        var fullPath = Path.GetFullPath(path);
        var fullWorkspace = Path.GetFullPath(workspacePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(fullPath, fullWorkspace, comparison))
            return "/workspace";

        var workspacePrefix = fullWorkspace + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(workspacePrefix, comparison))
            return path;

        var relative = Path.GetRelativePath(fullWorkspace, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

        return $"/workspace/{relative}";
    }

    private static string BuildEvidenceSummary(
        TestMap.Services.TestGeneration.TargetSelection.CandidateMethodContext? context)
    {
        if (context == null)
            return "No method context was available.";

        return string.Join(
            Environment.NewLine,
            [
                $"Context evidence: {context.ContextEvidenceKind}",
                context.ContextEvidenceSummary,
                $"Access path: {context.AccessPathSummary}",
                $"Candidate intentions: {context.CandidateTestIntentionsSummary}",
                $"Type construction: {context.CandidateTypeConstructionSummary}",
                $"Dependencies: {context.TestDependencies}"
            ]);
    }
}
