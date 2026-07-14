using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.Experiment.Reporting;

namespace TestMap.UnitTests.TestGeneration;

public sealed class ExperimentResultsWriterTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveResultsFilePath_UsesDefaultWhenOutputPathIsMissing()
    {
        var path = ExperimentResultsWriter.ResolveResultsFilePath(new ExperimentConfig());

        Assert.Equal(Path.Combine("Output", "experiment-results.csv"), path);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveResultsFilePath_TreatsCsvOutputPathAsExactFilePath()
    {
        var path = Path.Combine("custom", "results.csv");

        var resolved = ExperimentResultsWriter.ResolveResultsFilePath(new ExperimentConfig { OutputPath = path });

        Assert.Equal(path, resolved);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveResultsFilePath_TreatsNonCsvOutputPathAsDirectory()
    {
        var outputDirectory = Path.Combine("custom", "output");

        var resolved = ExperimentResultsWriter.ResolveResultsFilePath(
            new ExperimentConfig { OutputPath = outputDirectory });

        Assert.Equal(Path.Combine(outputDirectory, "experiment-results.csv"), resolved);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_WritesHeaderAndEscapedRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var writer = new ExperimentResultsWriter();

        try
        {
            await writer.WriteAsync(
                new ExperimentRun { Id = 42, RunUid = "run-42", ResultsFilePath = path },
                [
                    new ExperimentResultFileRow
                    {
                        AttemptId = "attempt-42",
                        ExperimentRunId = 42,
                        ExperimentRunUid = "run-42",
                        ExperimentSeriesId = "model-study",
                        CandidateCohortId = 5,
                        CandidateCohortMemberId = 9,
                        RepoOwner = "owner",
                        RepoName = "repo",
                        SourceMethodName = "Method,WithComma",
                        SourceMethodSignature = "void Method()",
                        CandidateTestIntentionsSummary = "- Tests branch A",
                        CandidateTypeConstructionSummary = "SUT: Demo",
                        CandidateMetadataJson = "{\"intentions\":1}",
                        Provider = AiProvider.OpenAi,
                        GenerationApproach = TestGenerationApproach.MetricsDriven,
                        ContextMode = GenerationContextMode.NoHistory,
                        BudgetMode = GenerationBudgetMode.PassAt1,
                        SourceMemberVisibility = "Private",
                        AccessStrategy = "PublicCallerPath",
                        AccessPathMemberIds = "12>10",
                        TestMappingCount = 1,
                        SetupBindingCount = 2,
                        RunDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
                        GeneratedTestCompiled = true,
                        GeneratedTestExecuted = true,
                        GeneratedTestPassed = true,
                        GenerationDurationSeconds = 1.25,
                        ValidationDurationSeconds = 2.5,
                        TotalAttemptDurationSeconds = 3.75,
                        GeneratedTestExecutionTimeMs = 12.5
                    }
                ]);

            var text = await File.ReadAllTextAsync(path);

            Assert.Contains("results_schema_version,row_kind,attempt_id,experiment_run_id,experiment_run_uid,experiment_series_id,candidate_cohort_id,candidate_cohort_member_id,producer_lane", text);
            Assert.Contains("2.0,attempt,attempt-42,42,run-42,model-study,5,9,testmap", text);
            Assert.DoesNotContain("metrics_path", text);
            Assert.Contains("source_method_mi,source_method_cc,source_method_coupling,source_method_dit,source_method_sloc,source_method_eloc", text);
            Assert.Contains("baseline_test_mi,baseline_test_cc,baseline_test_coupling,baseline_test_dit,baseline_test_sloc,baseline_test_eloc", text);
            Assert.Contains("generated_test_mi,generated_test_cc,generated_test_coupling,generated_test_dit,generated_test_sloc,generated_test_eloc", text);
            Assert.Contains("roslyn_diagnostics_before_raw_count,roslyn_diagnostics_after_raw_count,new_actionable_roslyn_diagnostics_count", text);
            Assert.Contains("source_member_visibility,access_strategy,access_path_member_ids,test_mapping_count,setup_binding_count", text);
            Assert.Contains("candidate_test_intentions_summary,candidate_type_construction_summary,candidate_metadata_json", text);
            Assert.Contains(
                "generation_duration_seconds,validation_duration_seconds,total_attempt_duration_seconds,baseline_test_execution_time_ms,generated_test_execution_time_ms",
                text);
            Assert.Contains(",1.25,2.5,3.75,,12.5,", text);
            Assert.Contains("tool_observed_outcome", text);
            Assert.Contains("testmap", text);
            Assert.DoesNotContain(",classification,", text);
            Assert.Contains("\"Method,WithComma\"", text);
            Assert.Contains("\"{\"\"intentions\"\":1}\"", text);
            Assert.Contains("Private,PublicCallerPath,12>10,1,2", text);
        }
        finally
        {
            DeleteResultFiles(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AppendAsync_DoesNotDuplicateCanonicalAttempt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var writer = new ExperimentResultsWriter();

        try
        {
            var run = new ExperimentRun { Id = 7, RunUid = "run-7", ResultsFilePath = path };
            var row = new ExperimentResultFileRow
            {
                AttemptId = "attempt-7",
                ExperimentRunId = 7,
                ExperimentRunUid = "run-7",
                Provider = AiProvider.OpenAi,
                GenerationApproach = TestGenerationApproach.Naive,
                ContextMode = GenerationContextMode.ChainedHistory,
                BudgetMode = GenerationBudgetMode.PassAt1,
                RunDate = DateTime.UtcNow
            };

            await writer.AppendAsync(run, row);
            await writer.AppendAsync(run, row);

            var lines = await File.ReadAllLinesAsync(path);

            Assert.Equal(2, lines.Length);
            Assert.Single(lines, x => x.StartsWith("results_schema_version", StringComparison.Ordinal));
        }
        finally
        {
            DeleteResultFiles(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AppendAsync_DoesNotDuplicateCanonicalGeneratedTest()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var writer = new ExperimentResultsWriter();

        try
        {
            var run = new ExperimentRun { Id = 7, RunUid = "run-7", ResultsFilePath = path };
            var row = new ExperimentResultFileRow
            {
                RowKind = "generated_test",
                AttemptId = "attempt-7",
                ExperimentRunId = 7,
                ExperimentRunUid = "run-7",
                GeneratedTestMemberId = 42,
                GeneratedTestMethodName = "GeneratedTest",
                Provider = AiProvider.OpenAi,
                GenerationApproach = TestGenerationApproach.Naive,
                ContextMode = GenerationContextMode.ChainedHistory,
                BudgetMode = GenerationBudgetMode.PassAt1,
                RunDate = DateTime.UtcNow
            };

            await writer.AppendAsync(run, row);
            await writer.AppendAsync(run, row);

            var generatedLines = await File.ReadAllLinesAsync(
                ExperimentResultsWriter.ResolveGeneratedTestsPath(run));
            Assert.Equal(2, generatedLines.Length);
        }
        finally
        {
            DeleteResultFiles(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AppendAsync_ResumeScanHandlesQuotedMultilineFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var writer = new ExperimentResultsWriter();
        var run = new ExperimentRun { Id = 7, RunUid = "run-7", ResultsFilePath = path };
        var row = new ExperimentResultFileRow
        {
            RowKind = "generated_test",
            AttemptId = "attempt-multiline",
            ExperimentRunId = 7,
            ExperimentRunUid = "run-7",
            GeneratedTestMemberId = 42,
            GeneratedTestMethodName = "GeneratedTest",
            FailureSummary = "first line, with comma\nsecond line with \"quotes\"",
            Provider = AiProvider.OpenAi,
            GenerationApproach = TestGenerationApproach.Naive,
            ContextMode = GenerationContextMode.ChainedHistory,
            BudgetMode = GenerationBudgetMode.PassAt1,
            RunDate = DateTime.UtcNow
        };

        try
        {
            await writer.AppendAsync(run, row);
            var generatedPath = ExperimentResultsWriter.ResolveGeneratedTestsPath(run);
            ExperimentResultsWriter.ResetAppendCache(generatedPath);
            await writer.AppendAsync(run, row);

            var text = await File.ReadAllTextAsync(generatedPath);
            Assert.Equal(1, text.Split(",generated_test,attempt-multiline,").Length - 1);
        }
        finally
        {
            DeleteResultFiles(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_SeparatesAttemptGeneratedTestAndTestResultGrains()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var writer = new ExperimentResultsWriter();
        var run = new ExperimentRun { Id = 8, RunUid = "run-8", ResultsFilePath = path };

        try
        {
            await writer.WriteAsync(
                run,
                [
                    new ExperimentResultFileRow
                    {
                        RowKind = "attempt",
                        AttemptId = "attempt-8",
                        ExperimentRunId = 8,
                        ExperimentRunUid = "run-8",
                        GeneratedTestMemberId = 101
                    },
                    new ExperimentResultFileRow
                    {
                        RowKind = "generated_test",
                        AttemptId = "attempt-8",
                        ExperimentRunId = 8,
                        ExperimentRunUid = "run-8",
                        GeneratedTestMemberId = 102,
                        ImpactAttribution = "attempt_level",
                        CoverageDelta = 0.05
                    },
                    new ExperimentResultFileRow
                    {
                        RowKind = "test_result",
                        AttemptId = "attempt-8",
                        ExperimentRunId = 8,
                        ExperimentRunUid = "run-8",
                        GeneratedTestMemberId = 102,
                        TestResultId = 501
                    }
                ]);

            var attemptLines = await File.ReadAllLinesAsync(path);
            var generatedLines = await File.ReadAllLinesAsync(ExperimentResultsWriter.ResolveGeneratedTestsPath(run));
            var resultLines = await File.ReadAllLinesAsync(ExperimentResultsWriter.ResolveTestResultsPath(run));
            var manifest = await File.ReadAllTextAsync(ExperimentResultsWriter.ResolveManifestPath(run));

            Assert.Equal(2, attemptLines.Length);
            Assert.Equal(3, generatedLines.Length);
            Assert.Equal(2, resultLines.Length);
            Assert.All(generatedLines.Skip(1), line => Assert.Contains(",generated_test,attempt-8,", line));
            Assert.Contains(",test_result,attempt-8,", resultLines[1]);
            Assert.Contains("\"coverage_unit\": \"fraction\"", manifest);
            Assert.Contains("\"mutation_score_unit\": \"percentage_points\"", manifest);
        }
        finally
        {
            DeleteResultFiles(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_RejectsRowsWithoutGlobalIdentity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var writer = new ExperimentResultsWriter();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync(
            new ExperimentRun { Id = 9, RunUid = "run-9", ResultsFilePath = path },
            [new ExperimentResultFileRow { ExperimentRunId = 9 }]));

        Assert.Contains("attempt_id", error.Message);
        DeleteResultFiles(path);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_RejectsDuplicateAttemptRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var writer = new ExperimentResultsWriter();
        var row = new ExperimentResultFileRow
        {
            AttemptId = "duplicate",
            ExperimentRunId = 10,
            ExperimentRunUid = "run-10"
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync(
            new ExperimentRun { Id = 10, RunUid = "run-10", ResultsFilePath = path },
            [row, row]));

        Assert.Contains("duplicate attempt_id", error.Message);
        DeleteResultFiles(path);
    }

    private static void DeleteResultFiles(string path)
    {
        var run = new ExperimentRun { ResultsFilePath = path };
        foreach (var candidate in new[]
                 {
                     path,
                     ExperimentResultsWriter.ResolveGeneratedTestsPath(run),
                     ExperimentResultsWriter.ResolveTestResultsPath(run),
                     ExperimentResultsWriter.ResolveManifestPath(run)
                 })
        {
            if (File.Exists(candidate)) File.Delete(candidate);
        }
    }
}
