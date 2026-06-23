using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.AgentTools;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.AgentTools;
using TestMap.Services.AgentTools;
using TestMap.Services.Experiment.Evaluation;
using TestMap.Services.Experiment.Evaluation.AgentTools;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="AgentToolEvaluationLane.ExecuteAsync"/> covering the happy path,
/// no-change result, exception handling, and timeout classification.
/// Uses a local test runner and an in-memory SQLite database.
/// </summary>
public sealed class AgentToolEvaluationLaneTests
{
    private static ExperimentToolConfig MakeToolConfig() =>
        new() { Id = "codex", TimeoutMinutes = 45 };

    private static ExperimentEvaluationWorkItemContext MakeContext(
        int experimentRunId,
        int workItemId,
        int candidateId,
        int? targetedBaselineId = null) => new()
    {
        WorkItem = new ExperimentMatrixWorkItem
        {
            Id = workItemId,
            ExperimentRunId = experimentRunId,
            CandidateMethodId = candidateId
        },
        Candidate = new CandidateMethod { Id = candidateId },
        TargetedBaselineId = targetedBaselineId
    };

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<(int RunId, int WorkItemId, int CandidateId)> SeedGraphAsync(
        TestMapDbContext db)
    {
        var run = new ExperimentRunEntity
        {
            ProjectId = 1,
            StartTime = DateTime.UtcNow,
            Objective = "TestSuiteExpansion",
            CandidateSelectionStrategy = "Existing",
            Configuration = "{}",
            ResultsFilePath = string.Empty,
            Status = "Running"
        };
        db.ExperimentRuns.Add(run);
        await db.SaveChangesAsync();

        var candidate = new CandidateMethodEntity
        {
            ExperimentRunId = run.Id,
            SourceMemberId = 1,
            SourceMethodName = "Foo",
            SourceMethodSignature = "public void Foo()"
        };
        db.CandidateMethods.Add(candidate);
        await db.SaveChangesAsync();

        var workItem = new ExperimentMatrixWorkItemEntity
        {
            ExperimentRunId = run.Id,
            CandidateMethodId = candidate.Id,
            MemberId = 1,
            StableKey = "key",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.ExperimentMatrixWorkItems.Add(workItem);
        await db.SaveChangesAsync();

        return (run.Id, workItem.Id, candidate.Id);
    }

    /// <summary>
    /// ExecuteAsync with a successful mock run (changed files) persists a Completed attempt
    /// and returns Success = true.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_SuccessfulRunWithChanges_PersistsCompletedAttempt()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, workItemId, candidateId) = await SeedGraphAsync(db);

        var runner = new TestAgentToolRunner
        {
            ChangedFiles = ["SomeTests.cs"],
            PatchDiff = "diff content"
        };
        var repo = new ToolAttemptRepository(db);
        var lane = new AgentToolEvaluationLane(runner, new AgentToolEnvironmentResolver(), repo,
            [MakeToolConfig()]);
        var context = MakeContext(runId, workItemId, candidateId, targetedBaselineId: 44);

        // Act
        var result = await lane.ExecuteAsync(context, default);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.ChangedFiles); // lane exposes the changed-files list for post-processing
        db.ChangeTracker.Clear();
        var attempt = await db.ToolAttempts.SingleAsync();
        Assert.Equal(ToolRunStatus.Completed.ToString(), attempt.RunStatus);
        Assert.Equal(44, attempt.TargetedBaselineId);
        Assert.Equal(1, attempt.ChangedFilesCount);
        Assert.Equal(1, attempt.TestFilesChanged);
        Assert.Equal(ToolValidationOutcome.NotEvaluated.ToString(), attempt.ValidationOutcome);
        Assert.Equal(ToolObservedOutcome.ChangedNotValidated.ToString(), attempt.ObservedOutcome);
    }

    /// <summary>
    /// ExecuteAsync with a run that produces no changed files persists a CompletedNoChange attempt.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_NoChangedFiles_PersistsCompletedNoChangeAttempt()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, workItemId, candidateId) = await SeedGraphAsync(db);

        var runner = new TestAgentToolRunner { ChangedFiles = [] };
        var repo = new ToolAttemptRepository(db);
        var lane = new AgentToolEvaluationLane(runner, new AgentToolEnvironmentResolver(), repo,
            [MakeToolConfig()]);
        var context = MakeContext(runId, workItemId, candidateId);

        // Act
        var result = await lane.ExecuteAsync(context, default);

        // Assert
        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var attempt = await db.ToolAttempts.SingleAsync();
        Assert.Equal(ToolRunStatus.CompletedNoChange.ToString(), attempt.RunStatus);
        Assert.Equal(ToolObservedOutcome.NoChange.ToString(), attempt.ObservedOutcome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_OnlyAgentMetadataChanged_PersistsCompletedNoChangeAttempt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, workItemId, candidateId) = await SeedGraphAsync(db);

        var runner = new TestAgentToolRunner
        {
            ChangedFiles =
            [
                ".testmap/prompt.md",
                ".testmap/task-card.json",
                ".codex/session.json"
            ]
        };
        var repo = new ToolAttemptRepository(db);
        var lane = new AgentToolEvaluationLane(runner, new AgentToolEnvironmentResolver(), repo,
            [MakeToolConfig()]);

        var result = await lane.ExecuteAsync(
            MakeContext(runId, workItemId, candidateId),
            default);

        Assert.True(result.Success);
        Assert.Empty(result.ChangedFiles);
        db.ChangeTracker.Clear();
        var attempt = await db.ToolAttempts.SingleAsync();
        Assert.Equal(ToolRunStatus.CompletedNoChange.ToString(), attempt.RunStatus);
        Assert.Equal(0, attempt.ChangedFilesCount);
        Assert.Equal(0, attempt.TestFilesChanged);
        Assert.Equal(0, attempt.ProductionFilesChanged);
        Assert.Equal(ToolObservedOutcome.NoChange.ToString(), attempt.ObservedOutcome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_MixedRepositoryAndMetadataChanges_CountsOnlyRepositoryFiles()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, workItemId, candidateId) = await SeedGraphAsync(db);

        var runner = new TestAgentToolRunner
        {
            ChangedFiles =
            [
                ".testmap/evidence-summary.md",
                ".claude/settings.local.json",
                "Tests/AddedTests.cs"
            ]
        };
        var repo = new ToolAttemptRepository(db);
        var lane = new AgentToolEvaluationLane(runner, new AgentToolEnvironmentResolver(), repo,
            [MakeToolConfig()]);

        var result = await lane.ExecuteAsync(
            MakeContext(runId, workItemId, candidateId),
            default);

        Assert.True(result.Success);
        Assert.Equal(["Tests/AddedTests.cs"], result.ChangedFiles);
        db.ChangeTracker.Clear();
        var attempt = await db.ToolAttempts.SingleAsync();
        Assert.Equal(ToolRunStatus.Completed.ToString(), attempt.RunStatus);
        Assert.Equal(1, attempt.ChangedFilesCount);
        Assert.Equal(1, attempt.TestFilesChanged);
        Assert.Equal(0, attempt.ProductionFilesChanged);
    }

    /// <summary>
    /// ExecuteAsync when the runner throws persists a ToolCrashed attempt and returns
    /// Success = false with the error message.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_RunnerThrows_PersistsToolCrashedAttempt()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, workItemId, candidateId) = await SeedGraphAsync(db);

        var runner = new ThrowingTestRunner("Docker daemon not reachable");
        var repo = new ToolAttemptRepository(db);
        var lane = new AgentToolEvaluationLane(runner, new AgentToolEnvironmentResolver(), repo,
            [MakeToolConfig()]);
        var context = MakeContext(runId, workItemId, candidateId);

        // Act
        var result = await lane.ExecuteAsync(context, default);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Docker daemon", result.ErrorMessage);
        db.ChangeTracker.Clear();
        var attempt = await db.ToolAttempts.SingleAsync();
        Assert.Equal(ToolRunStatus.ToolCrashed.ToString(), attempt.RunStatus);
        Assert.Contains("Docker daemon", attempt.Notes);
    }

    /// <summary>
    /// ExecuteAsync with a timed-out runner result persists a TimedOut attempt.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_Timeout_PersistsTimedOutAttempt()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, workItemId, candidateId) = await SeedGraphAsync(db);

        var runner = new TestAgentToolRunner { TimedOut = true };
        var repo = new ToolAttemptRepository(db);
        var lane = new AgentToolEvaluationLane(runner, new AgentToolEnvironmentResolver(), repo,
            [MakeToolConfig()]);
        var context = MakeContext(runId, workItemId, candidateId);

        // Act
        var result = await lane.ExecuteAsync(context, default);

        // Assert
        db.ChangeTracker.Clear();
        var attempt = await db.ToolAttempts.SingleAsync();
        Assert.Equal(ToolRunStatus.TimedOut.ToString(), attempt.RunStatus);
        Assert.Equal(124, attempt.ExitCode);
        Assert.Equal(ToolValidationOutcome.TimedOut.ToString(), attempt.ValidationOutcome);
        Assert.Equal(ToolObservedOutcome.TimedOut.ToString(), attempt.ObservedOutcome);
        Assert.False(result.Success);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_NonZeroExit_PersistsToolCrashedAttempt()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, workItemId, candidateId) = await SeedGraphAsync(db);

        var runner = new TestAgentToolRunner { ExitCode = 2, ChangedFiles = ["SomeTests.cs"] };
        var repo = new ToolAttemptRepository(db);
        var lane = new AgentToolEvaluationLane(runner, new AgentToolEnvironmentResolver(), repo,
            [MakeToolConfig()]);
        var context = MakeContext(runId, workItemId, candidateId);

        // Act
        var result = await lane.ExecuteAsync(context, default);

        // Assert
        Assert.False(result.Success);
        db.ChangeTracker.Clear();
        var attempt = await db.ToolAttempts.SingleAsync();
        Assert.Equal(ToolRunStatus.ToolCrashed.ToString(), attempt.RunStatus);
        Assert.Equal(ToolValidationOutcome.ToolFailed.ToString(), attempt.ValidationOutcome);
        Assert.Equal(ToolObservedOutcome.ToolFailed.ToString(), attempt.ObservedOutcome);
        Assert.Equal(1, attempt.ChangedFilesCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_CodexTurnCompleted_ReturnsInputAndOutputTokens()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "codex.events.jsonl"),
                "{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":208522,\"cached_input_tokens\":189184,\"output_tokens\":2933,\"reasoning_output_tokens\":0}}");

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "codex");

            Assert.NotNull(usage);
            Assert.Equal(208522, usage.InputTokens);
            Assert.Equal(2933, usage.OutputTokens);
            Assert.Equal("codex.events.jsonl:turn.completed", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_ClaudeResult_IncludesCacheInputTokens()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "claude.events.jsonl"),
                "{\"type\":\"result\",\"subtype\":\"success\",\"total_cost_usd\":0.20903225,\"usage\":{\"input_tokens\":1666,\"cache_creation_input_tokens\":11481,\"cache_read_input_tokens\":109708,\"output_tokens\":2859}}");

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "claude");

            Assert.NotNull(usage);
            Assert.Equal(122855, usage.InputTokens);
            Assert.Equal(2859, usage.OutputTokens);
            Assert.Equal("claude.events.jsonl:result", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_GeminiStreamJsonResult_SumsModelTokenStats()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "gemini.events.jsonl"),
                """
                {"type":"init","model":"gemini-2.5-pro"}
                {"type":"result","response":"done","stats":{"models":{"gemini-2.5-pro":{"tokens":{"prompt":24939,"candidates":20,"total":25113,"cached":21263,"thoughts":154,"tool":0}},"gemini-2.5-flash":{"tokens":{"prompt":8965,"candidates":10,"total":9033,"cached":0,"thoughts":30,"tool":28}}}}}
                """);

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "gemini");

            Assert.NotNull(usage);
            Assert.Equal(33904, usage.InputTokens);
            Assert.Equal(242, usage.OutputTokens);
            Assert.Equal("gemini.events.jsonl:result.stats.models", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_GeminiStreamJsonResult_ParsesCurrentTopLevelTokenStats()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "gemini.events.jsonl"),
                """
                {"type":"result","timestamp":"2026-06-05T18:07:27.926Z","status":"error","stats":{"total_tokens":96664,"input_tokens":92798,"output_tokens":1569,"cached":65372,"input":27426,"duration_ms":37313,"tool_calls":5,"models":{"gemini-2.5-pro":{"total_tokens":96664,"input_tokens":92798,"output_tokens":1569,"cached":65372,"input":27426}}}}
                """);

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "gemini");

            Assert.NotNull(usage);
            Assert.Equal(92798, usage.InputTokens);
            Assert.Equal(3866, usage.OutputTokens);
            Assert.Equal("gemini.events.jsonl:result.stats", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_OpenHandsPersistedState_SumsConversationMetrics()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            var conversationPath = Path.Combine(artifactPath, "openhands-state", "conversations", "abc123");
            Directory.CreateDirectory(conversationPath);
            File.WriteAllText(
                Path.Combine(conversationPath, "base_state.json"),
                """
                {
                  "stats": {
                    "usage_to_metrics": {
                      "agent": {
                        "accumulated_token_usage": {
                          "prompt_tokens": 100,
                          "completion_tokens": 25,
                          "cache_read_tokens": 40,
                          "cache_write_tokens": 5,
                          "reasoning_tokens": 7
                        }
                      },
                      "condenser": {
                        "accumulated_token_usage": {
                          "prompt_tokens": 11,
                          "completion_tokens": 3
                        }
                      }
                    }
                  }
                }
                """);

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "openhands");

            Assert.NotNull(usage);
            Assert.Equal(156, usage.InputTokens);
            Assert.Equal(35, usage.OutputTokens);
            Assert.EndsWith("base_state.json:stats.usage_to_metrics", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_AiderStdoutTokenLine_ReturnsSentAndReceivedTokens()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "aider.stdout.log"),
                """
                Some regular aider output.
                Tokens: 3.2k sent, 1.1k received. Cost: $0.03 message, $0.04 session.
                """);

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "aider");

            Assert.NotNull(usage);
            Assert.Equal(3200, usage.InputTokens);
            Assert.Equal(1100, usage.OutputTokens);
            Assert.Equal("aider.stdout.log:tokens-line", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_AiderStdoutWithMultipleTokenLines_UsesLatest()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "aider.stdout.log"),
                """
                Tokens: 800 sent, 200 received. Cost: $0.01 message.
                Tokens: 4.5k sent, 900 received. Cost: $0.02 message, $0.03 session.
                """);

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "aider");

            Assert.NotNull(usage);
            Assert.Equal(4500, usage.InputTokens);
            Assert.Equal(900, usage.OutputTokens);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_CopilotOtelTokenMetric_ReturnsInputAndOutputTokens()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "copilot-otel.jsonl"),
                """
                {"name":"gen_ai.client.token.usage","data":{"dataPoints":[{"sum":330400,"attributes":[{"key":"gen_ai.token.type","value":{"stringValue":"input"}}]},{"sum":6200,"attributes":[{"key":"gen_ai.token.type","value":{"stringValue":"output"}}]}]}}
                """);

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "copilot");

            Assert.NotNull(usage);
            Assert.Equal(330400, usage.InputTokens);
            Assert.Equal(6200, usage.OutputTokens);
            Assert.Equal("copilot-otel.jsonl:gen_ai.client.token.usage", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_CopilotOtelHistogramMetric_UsesLatestCumulativeSums()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "copilot-otel.jsonl"),
                """
                {"type":"metric","name":"gen_ai.client.token.usage","dataPoints":[{"attributes":{"gen_ai.token.type":"input"},"value":{"count":5,"sum":125033.0}},{"attributes":{"gen_ai.token.type":"output"},"value":{"count":5,"sum":3386.0}}]}
                {"type":"metric","name":"gen_ai.client.token.usage","dataPoints":[{"attributes":{"gen_ai.token.type":"input"},"value":{"count":10,"sum":285026.0}},{"attributes":{"gen_ai.token.type":"output"},"value":{"count":10,"sum":5340.0}}]}
                """);

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "copilot");

            Assert.NotNull(usage);
            Assert.Equal(285026, usage.InputTokens);
            Assert.Equal(5340, usage.OutputTokens);
            Assert.Equal("copilot-otel.jsonl:gen_ai.client.token.usage", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_CopilotOtelSpanFallback_DoesNotDoubleCountCacheDetails()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "copilot-otel.jsonl"),
                """
                {"type":"span","name":"chat claude-haiku-4.5","attributes":{"gen_ai.usage.input_tokens":23056,"gen_ai.usage.output_tokens":513,"gen_ai.usage.cache_creation_input_tokens":23046,"gen_ai.usage.reasoning_output_tokens":285}}
                {"type":"span","name":"chat claude-haiku-4.5","attributes":{"gen_ai.usage.input_tokens":23737,"gen_ai.usage.output_tokens":124,"gen_ai.usage.cache_read_input_tokens":23046,"gen_ai.usage.cache_creation_input_tokens":684,"gen_ai.usage.reasoning_output_tokens":124}}
                """);

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "copilot");

            Assert.NotNull(usage);
            Assert.Equal(46793, usage.InputTokens);
            Assert.Equal(637, usage.OutputTokens);
            Assert.Equal("copilot-otel.jsonl:spans", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_CopilotStderrTokenFooter_ReturnsInputAndOutputTokens()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "copilot.stderr.log"),
                "Tokens     ↑ 330.4k (292.2k cached, 37.4k written) \u2022 ↓ 6.2k (2.4k reasoning)");

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "copilot");

            Assert.NotNull(usage);
            Assert.Equal(330400, usage.InputTokens);
            Assert.Equal(6200, usage.OutputTokens);
            Assert.Equal("copilot.stderr.log:tokens-line", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExtractUsage_MiniSweTrajectory_SumsUsageEntries()
    {
        var artifactPath = CreateTempArtifactDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(artifactPath, "mini-swe-agent-trajectory.json"),
                """
                {
                  "info": {
                    "model_stats": {
                      "api_calls": 2
                    }
                  },
                  "trajectory": [
                    {
                      "response": {
                        "usage": {
                          "completion_tokens": 166,
                          "prompt_tokens": 3481,
                          "total_tokens": 3647,
                          "prompt_tokens_details": {
                            "cached_tokens": 0,
                            "cache_creation_tokens": 3478
                          }
                        }
                      }
                    },
                    {
                      "response": {
                        "usage": {
                          "completion_tokens": 473,
                          "prompt_tokens": 14581,
                          "total_tokens": 15054,
                          "prompt_tokens_details": {
                            "cached_tokens": 14163,
                            "cache_creation_tokens": 417
                          }
                        }
                      }
                    }
                  ]
                }
                """);

            var usage = AgentToolEvaluationLane.ExtractUsage(artifactPath, "mini-swe-agent");

            Assert.NotNull(usage);
            Assert.Equal(18062, usage.InputTokens);
            Assert.Equal(639, usage.OutputTokens);
            Assert.Equal("mini-swe-agent-trajectory.json:usage", usage.Source);
        }
        finally
        {
            Directory.Delete(artifactPath, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_UsageJsonl_PersistsTokenUsage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, workItemId, candidateId) = await SeedGraphAsync(db);

        var runner = new TestAgentToolRunner
        {
            ChangedFiles = ["SomeTests.cs"],
            JsonlFileName = "codex.events.jsonl",
            JsonlContent = "{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":100,\"cached_input_tokens\":80,\"output_tokens\":25}}"
        };
        var repo = new ToolAttemptRepository(db);
        var lane = new AgentToolEvaluationLane(runner, new AgentToolEnvironmentResolver(), repo,
            [MakeToolConfig()]);

        var result = await lane.ExecuteAsync(MakeContext(runId, workItemId, candidateId), default);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var attempt = await db.ToolAttempts.SingleAsync();
        Assert.True(attempt.JsonlLogAvailable);
        Assert.Equal(
            Path.Combine(attempt.ArtifactPath, "codex.events.jsonl"),
            attempt.StdOutLogPath);
        Assert.Equal(
            Path.Combine(attempt.ArtifactPath, "codex.stderr.log"),
            attempt.StdErrLogPath);
        Assert.Equal(attempt.StdOutLogPath, attempt.JsonlLogPath);
        Assert.True(attempt.UsageAvailable);
        Assert.Equal("codex.events.jsonl:turn.completed", attempt.UsageSource);
        Assert.Equal(100, attempt.InputTokens);
        Assert.Equal(25, attempt.OutputTokens);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_MalformedJsonl_DoesNotMarkJsonlAvailable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, workItemId, candidateId) = await SeedGraphAsync(db);

        var runner = new TestAgentToolRunner
        {
            ChangedFiles = ["SomeTests.cs"],
            JsonlFileName = "openhands.events.jsonl",
            JsonlContent = "OpenHands plain text banner\nstill not json\n"
        };
        var repo = new ToolAttemptRepository(db);
        var lane = new AgentToolEvaluationLane(runner, new AgentToolEnvironmentResolver(), repo,
            [new ExperimentToolConfig { Id = "openhands", TimeoutMinutes = 45 }]);

        var result = await lane.ExecuteAsync(MakeContext(runId, workItemId, candidateId), default);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var attempt = await db.ToolAttempts.SingleAsync();
        Assert.False(attempt.JsonlLogAvailable);
        Assert.Equal(
            Path.Combine(attempt.ArtifactPath, "openhands.stdout.log"),
            attempt.StdOutLogPath);
        Assert.Equal(
            Path.Combine(attempt.ArtifactPath, "openhands.stderr.log"),
            attempt.StdErrLogPath);
        Assert.Equal(
            Path.Combine(attempt.ArtifactPath, "openhands.events.jsonl"),
            attempt.JsonlLogPath);
        Assert.False(attempt.UsageAvailable);
    }

    // ─── Test helpers ─────────────────────────────────────────────────────────

    private static string CreateTempArtifactDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-tool-usage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestAgentToolRunner : IAgentToolRunner
    {
        public bool TimedOut { get; init; }
        public int ExitCode { get; init; }
        public IReadOnlyList<string> ChangedFiles { get; init; } = [];
        public string PatchDiff { get; init; } = string.Empty;
        public string JsonlFileName { get; init; } = string.Empty;
        public string JsonlContent { get; init; } = string.Empty;

        public Task<ToolAvailabilityResult> CheckAvailabilityAsync(
            ExperimentToolConfig tool, CancellationToken ct) =>
            Task.FromResult(new ToolAvailabilityResult { ToolId = tool.Id, IsAvailable = true });

        public Task<ToolRunPreparationResult> PrepareAsync(ToolRunRequest request, CancellationToken ct) =>
            Task.FromResult(new ToolRunPreparationResult { Success = true });

        public async Task<ToolRunResult> RunAsync(ToolRunRequest request, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(JsonlFileName))
            {
                Directory.CreateDirectory(request.ArtifactPath);
                await File.WriteAllTextAsync(
                    Path.Combine(request.ArtifactPath, JsonlFileName),
                    JsonlContent,
                    ct);
            }

            return new ToolRunResult
            {
                ExitCode = TimedOut ? 124 : ExitCode,
                Elapsed = TimeSpan.FromSeconds(5),
                TimedOut = TimedOut,
                ToolVersion = "test-runner"
            };
        }

        public Task<ToolRunCollectionResult> CollectAsync(ToolRunRequest request, CancellationToken ct) =>
            Task.FromResult(new ToolRunCollectionResult
            {
                PatchDiff = PatchDiff,
                ChangedFiles = ChangedFiles,
                GitStatusBefore = "nothing to commit",
                GitStatusAfter = ChangedFiles.Count > 0 ? "M some/file.cs" : "nothing to commit"
            });
    }

    /// <summary>Runner that throws on RunAsync to exercise the crash path.</summary>
    private sealed class ThrowingTestRunner : IAgentToolRunner
    {
        private readonly string _message;
        public ThrowingTestRunner(string message) => _message = message;

        public Task<ToolAvailabilityResult> CheckAvailabilityAsync(
            ExperimentToolConfig tool, CancellationToken ct) =>
            Task.FromResult(new ToolAvailabilityResult { ToolId = tool.Id, IsAvailable = true });

        public Task<ToolRunPreparationResult> PrepareAsync(ToolRunRequest request, CancellationToken ct) =>
            Task.FromResult(new ToolRunPreparationResult { Success = true });

        public Task<ToolRunResult> RunAsync(ToolRunRequest request, CancellationToken ct) =>
            throw new InvalidOperationException(_message);

        public Task<ToolRunCollectionResult> CollectAsync(ToolRunRequest request, CancellationToken ct) =>
            Task.FromResult(new ToolRunCollectionResult());
    }
}
