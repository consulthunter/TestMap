using TestMap.Models.Experiment;
using TestMap.Services.Experiment.Reporting;

namespace TestMap.UnitTests.TestGeneration;

public sealed class AssertionObservationWriterTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_PublishesDeterministicAssertionSidecar()
    {
        var resultsPath = Path.Combine(
            Path.GetTempPath(),
            $"testmap-assertions-{Guid.NewGuid():N}",
            "results.csv");
        var run = new ExperimentRun { Id = 7, RunUid = "run-7", ResultsFilePath = resultsPath };
        var row = MakeRow();

        try
        {
            await new AssertionObservationWriter().WriteAsync(run, [row]);

            var sidecarPath = ExperimentResultsWriter.ResolveAssertionsPath(run);
            var lines = await File.ReadAllLinesAsync(sidecarPath);
            Assert.Equal(2, lines.Length);
            Assert.StartsWith("assertion_schema_version,observation_id,attempt_id", lines[0]);
            Assert.StartsWith("1.0,31,attempt-7,7,run-7", lines[1]);
            Assert.Contains("\"[{\"\"step\"\":0,\"\"outcome\"\":\"\"Traced\"\"}]\"", lines[1]);
        }
        finally
        {
            var directory = Path.GetDirectoryName(resultsPath);
            if (directory != null && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_RejectsTracedRowsWithoutResolvedProductionMembers()
    {
        var resultsPath = Path.Combine(
            Path.GetTempPath(),
            $"testmap-assertions-{Guid.NewGuid():N}",
            "results.csv");
        var run = new ExperimentRun { Id = 7, RunUid = "run-7", ResultsFilePath = resultsPath };
        var row = MakeRow(resolvedProductionMemberIds: string.Empty);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AssertionObservationWriter().WriteAsync(run, [row]));

        Assert.Contains("resolved production member", error.Message);
    }

    private static AssertionObservationFileRow MakeRow(
        string resolvedProductionMemberIds = "19") => new()
    {
        ObservationId = 31,
        AttemptId = "attempt-7",
        ExperimentRunId = 7,
        ExperimentRunUid = "run-7",
        ExperimentSeriesId = "series-a",
        ProducerLane = "testmap",
        Model = "gpt",
        AttemptNumber = 1,
        RepositoryIdentity = "owner/repo",
        ResolvedCommit = "0123456789abcdef0123456789abcdef01234567",
        TargetId = new string('a', 64),
        TargetManifestSha256 = new string('b', 64),
        TargetSourceSha256 = new string('c', 64),
        CandidateMethodId = 19,
        IntendedSourceMemberId = 19,
        GeneratedTestAssertionSummaryId = 23,
        GeneratedTestExecutionId = 29,
        TestMemberId = 37,
        TestMemberName = "Calculate_returns_value",
        TestFilePath = "WidgetTests.cs",
        TestMemberContentHash = new string('d', 64),
        AssertionOrdinal = 0,
        StartLine = 10,
        StartColumn = 9,
        EndLine = 10,
        EndColumn = 34,
        Framework = "xunit",
        AssertionStyle = "Invocation",
        AssertionMethod = "Equal",
        RecognitionKind = "Semantic",
        ExpressionHash = new string('e', 64),
        Category = "Traced",
        ResolutionCode = "ProductionInvocation",
        TargetRelation = "Candidate",
        DepthReached = 1,
        ResolvedProductionMemberIds = resolvedProductionMemberIds,
        PolicyVersion = "assertion-lineage-v1",
        AssertionCatalogVersion = "assertion-catalog-v1",
        MaxDepth = 4,
        TraceSummary = "Assertion input reaches Widget.Calculate().",
        OrderedLineagePathsJson = "[{\"step\":0,\"outcome\":\"Traced\"}]",
        AnalyzedAt = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc)
    };
}
