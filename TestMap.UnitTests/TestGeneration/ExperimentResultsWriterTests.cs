using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.Experiment.Reporting;

namespace TestMap.UnitTests.TestGeneration;

public sealed class ExperimentResultsWriterTests
{
    private const string TargetId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string ManifestHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string SourceHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
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
                        TargetId = TargetId,
                        RepositoryIdentity = "owner/repo",
                        RequestedCommit = Commit,
                        ResolvedCommit = Commit,
                        CommitHash = Commit,
                        TargetManifestSha256 = ManifestHash,
                        TargetSourceSha256 = SourceHash,
                        ProvenancePolicyVersion = "pinned-target-v1",
                        WorkspaceIntegrityStatus = "VerifiedClean",
                        AssertionMeasurementStatus = "NotApplicable",
                        AssertionPolicyVersion = "assertion-lineage-v1",
                        AssertionCatalogVersion = "assertion-catalog-v1",
                        AssertionMaxDepth = 4,
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
            Assert.Contains("4.0,attempt,attempt-42,42,run-42,model-study,5,9,testmap", text);
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
                TargetId = TargetId, RepositoryIdentity = "owner/repo", RequestedCommit = Commit, ResolvedCommit = Commit, CommitHash = Commit,
                TargetManifestSha256 = ManifestHash, TargetSourceSha256 = SourceHash,
                ProvenancePolicyVersion = "pinned-target-v1", WorkspaceIntegrityStatus = "VerifiedClean",
                AssertionMeasurementStatus = "NotApplicable",
                AssertionPolicyVersion = "assertion-lineage-v1",
                AssertionCatalogVersion = "assertion-catalog-v1",
                AssertionMaxDepth = 4,
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
                TargetId = TargetId, RepositoryIdentity = "owner/repo", RequestedCommit = Commit, ResolvedCommit = Commit, CommitHash = Commit,
                TargetManifestSha256 = ManifestHash, TargetSourceSha256 = SourceHash,
                ProvenancePolicyVersion = "pinned-target-v1", WorkspaceIntegrityStatus = "VerifiedClean",
                AssertionMeasurementStatus = "NotApplicable",
                AssertionPolicyVersion = "assertion-lineage-v1",
                AssertionCatalogVersion = "assertion-catalog-v1",
                AssertionMaxDepth = 4,
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
            TargetId = TargetId, RepositoryIdentity = "owner/repo", RequestedCommit = Commit, ResolvedCommit = Commit, CommitHash = Commit,
            TargetManifestSha256 = ManifestHash, TargetSourceSha256 = SourceHash,
            ProvenancePolicyVersion = "pinned-target-v1", WorkspaceIntegrityStatus = "VerifiedClean",
            AssertionMeasurementStatus = "NotApplicable",
            AssertionPolicyVersion = "assertion-lineage-v1",
            AssertionCatalogVersion = "assertion-catalog-v1",
            AssertionMaxDepth = 4,
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
                        TargetId = TargetId, RepositoryIdentity = "owner/repo", RequestedCommit = Commit, ResolvedCommit = Commit, CommitHash = Commit,
                        TargetManifestSha256 = ManifestHash, TargetSourceSha256 = SourceHash,
                        ProvenancePolicyVersion = "pinned-target-v1", WorkspaceIntegrityStatus = "VerifiedClean",
                        AssertionMeasurementStatus = "NotApplicable",
                        AssertionPolicyVersion = "assertion-lineage-v1",
                        AssertionCatalogVersion = "assertion-catalog-v1",
                        AssertionMaxDepth = 4,
                        GeneratedTestMemberId = 101
                    },
                    new ExperimentResultFileRow
                    {
                        RowKind = "generated_test",
                        AttemptId = "attempt-8",
                        ExperimentRunId = 8,
                        ExperimentRunUid = "run-8",
                        TargetId = TargetId, RepositoryIdentity = "owner/repo", RequestedCommit = Commit, ResolvedCommit = Commit, CommitHash = Commit,
                        TargetManifestSha256 = ManifestHash, TargetSourceSha256 = SourceHash,
                        ProvenancePolicyVersion = "pinned-target-v1", WorkspaceIntegrityStatus = "VerifiedClean",
                        AssertionMeasurementStatus = "NotApplicable",
                        AssertionPolicyVersion = "assertion-lineage-v1",
                        AssertionCatalogVersion = "assertion-catalog-v1",
                        AssertionMaxDepth = 4,
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
                        TargetId = TargetId, RepositoryIdentity = "owner/repo", RequestedCommit = Commit, ResolvedCommit = Commit, CommitHash = Commit,
                        TargetManifestSha256 = ManifestHash, TargetSourceSha256 = SourceHash,
                        ProvenancePolicyVersion = "pinned-target-v1", WorkspaceIntegrityStatus = "VerifiedClean",
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
            ,TargetId = TargetId, RepositoryIdentity = "owner/repo", RequestedCommit = Commit, ResolvedCommit = Commit, CommitHash = Commit
            ,TargetManifestSha256 = ManifestHash, TargetSourceSha256 = SourceHash
            ,ProvenancePolicyVersion = "pinned-target-v1", WorkspaceIntegrityStatus = "VerifiedClean"
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync(
            new ExperimentRun { Id = 10, RunUid = "run-10", ResultsFilePath = path },
            [row, row]));

        Assert.Contains("duplicate attempt_id", error.Message);
        DeleteResultFiles(path);
    }

    [Fact]
    public async Task WriteAsync_RejectsValidatedSuccessWithBlockingIntegrity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var row = new ExperimentResultFileRow
        {
            AttemptId = "integrity-failed", ExperimentRunUid = "run-integrity", ExperimentRunId = 11,
            TargetId = TargetId, RepositoryIdentity = "owner/repo", RequestedCommit = Commit, ResolvedCommit = Commit, CommitHash = Commit,
            TargetManifestSha256 = ManifestHash, TargetSourceSha256 = SourceHash,
            ProvenancePolicyVersion = "pinned-target-v1", WorkspaceIntegrityStatus = "RevisionMismatch",
            ValidatedSuccess = true
        };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ExperimentResultsWriter().WriteAsync(new ExperimentRun { Id = 11, RunUid = "run-integrity", ResultsFilePath = path }, [row]));
        Assert.Contains("verified workspace integrity", error.Message, StringComparison.OrdinalIgnoreCase);
        DeleteResultFiles(path);
    }

    [Fact]
    public async Task WriteAsync_RejectsRequestedResolvedCommitMismatch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var row = new ExperimentResultFileRow
        {
            AttemptId = "revision-mismatch", ExperimentRunUid = "run-revision", ExperimentRunId = 12,
            TargetId = TargetId, RepositoryIdentity = "owner/repo", RequestedCommit = Commit,
            ResolvedCommit = "89abcdef0123456789abcdef0123456789abcdef",
            CommitHash = "89abcdef0123456789abcdef0123456789abcdef",
            TargetManifestSha256 = ManifestHash, TargetSourceSha256 = SourceHash,
            ProvenancePolicyVersion = "pinned-target-v1", WorkspaceIntegrityStatus = "VerifiedClean"
        };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ExperimentResultsWriter().WriteAsync(new ExperimentRun { Id = 12, RunUid = "run-revision", ResultsFilePath = path }, [row]));
        Assert.Contains("requested and resolved", error.Message, StringComparison.OrdinalIgnoreCase);
        DeleteResultFiles(path);
    }

    /// <summary>
    /// A row takes its counts from the per-test summary it describes, so its status must
    /// describe that same test. An attempt covering several tests can be Partial overall while
    /// an individual test has no usable evidence and therefore no counts; publishing that row
    /// under the attempt's status claims counts that reconcile when none exist.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_PartialStatusWithoutCounts_IsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var row = MakeAssertionRow("partial-no-counts", "Partial", "GeneratedTestMemberUnresolved");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ExperimentResultsWriter().WriteAsync(
                new ExperimentRun { Id = 14, RunUid = "run-14", ResultsFilePath = path },
                [row]));

        Assert.Contains("reconcile", error.Message, StringComparison.OrdinalIgnoreCase);
        DeleteResultFiles(path);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_UnavailableTestStatusWithoutCounts_IsAccepted()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var run = new ExperimentRun { Id = 15, RunUid = "run-15", ResultsFilePath = path };
        var row = MakeAssertionRow("unavailable-no-counts", "Unavailable", "GeneratedTestMemberUnresolved");

        try
        {
            await new ExperimentResultsWriter().WriteAsync(run, [row]);
            Assert.Contains(",Unavailable,GeneratedTestMemberUnresolved,", await File.ReadAllTextAsync(path));
        }
        finally
        {
            DeleteResultFiles(path);
        }
    }

    private static ExperimentResultFileRow MakeAssertionRow(
        string attemptId,
        string assertionStatus,
        string assertionReason) => new()
    {
        AttemptId = attemptId,
        ExperimentRunId = 14,
        ExperimentRunUid = "run-14",
        TargetId = TargetId,
        RepositoryIdentity = "owner/repo",
        RequestedCommit = Commit,
        ResolvedCommit = Commit,
        CommitHash = Commit,
        TargetManifestSha256 = ManifestHash,
        TargetSourceSha256 = SourceHash,
        ProvenancePolicyVersion = "pinned-target-v1",
        WorkspaceIntegrityStatus = "VerifiedClean",
        AssertionMeasurementStatus = assertionStatus,
        AssertionMeasurementReason = assertionReason,
        AssertionPolicyVersion = "assertion-lineage-v1",
        AssertionCatalogVersion = "assertion-catalog-v1",
        AssertionMaxDepth = 4,
        AssertionAttribution = "generated_test"
    };

    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_WritesSchema4AssertionSummaryAndManifestMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var run = new ExperimentRun { Id = 13, RunUid = "run-13", ResultsFilePath = path };
        var row = new ExperimentResultFileRow
        {
            AttemptId = "assertions-13",
            ExperimentRunId = 13,
            ExperimentRunUid = "run-13",
            TargetId = TargetId,
            RepositoryIdentity = "owner/repo",
            RequestedCommit = Commit,
            ResolvedCommit = Commit,
            CommitHash = Commit,
            TargetManifestSha256 = ManifestHash,
            TargetSourceSha256 = SourceHash,
            ProvenancePolicyVersion = "pinned-target-v1",
            WorkspaceIntegrityStatus = "VerifiedClean",
            AssertionMeasurementStatus = "Complete",
            AssertionPolicyVersion = "assertion-lineage-v1",
            AssertionCatalogVersion = "assertion-catalog-v1",
            AssertionMaxDepth = 4,
            RecognizedAssertionCount = 3,
            UnrecognizedAssertionCount = 1,
            TracedAssertionCount = 1,
            TrivialAssertionCount = 1,
            UnresolvedAssertionCount = 1,
            NoRecognizedAssertions = false,
            AssertionAnalysisDurationMs = 12.5,
            AssertionAttribution = "connectivity-only"
        };

        try
        {
            await new ExperimentResultsWriter().WriteAsync(run, [row]);

            var text = await File.ReadAllTextAsync(path);
            var manifest = await File.ReadAllTextAsync(
                ExperimentResultsWriter.ResolveManifestPath(run));
            Assert.Contains(
                "assertion_measurement_status,assertion_measurement_reason,assertion_policy_version",
                text);
            Assert.Contains(",Complete,,assertion-lineage-v1,assertion-catalog-v1,4,3,1,1,1,1,False,12.5,connectivity-only", text);
            Assert.Contains("\"results_schema_version\": \"4.0\"", manifest);
            Assert.Contains("\"assertion_schema_version\": \"1.0\"", manifest);
            Assert.Contains("\"assertion-lineage-v1\"", manifest);
            Assert.Contains("\"assertion-catalog-v1\"", manifest);
        }
        finally
        {
            DeleteResultFiles(path);
        }
    }

    private static void DeleteResultFiles(string path)
    {
        var run = new ExperimentRun { ResultsFilePath = path };
        foreach (var candidate in new[]
                 {
                     path,
                     ExperimentResultsWriter.ResolveGeneratedTestsPath(run),
                     ExperimentResultsWriter.ResolveTestResultsPath(run),
                     ExperimentResultsWriter.ResolveAssertionsPath(run),
                     ExperimentResultsWriter.ResolveManifestPath(run)
                 })
        {
            if (File.Exists(candidate)) File.Delete(candidate);
        }
    }
}
