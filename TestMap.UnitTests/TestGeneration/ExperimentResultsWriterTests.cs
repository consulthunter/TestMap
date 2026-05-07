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
                new ExperimentRun { Id = 42, ResultsFilePath = path },
                [
                    new ExperimentResultFileRow
                    {
                        ExperimentRunId = 42,
                        RepoOwner = "owner",
                        RepoName = "repo",
                        SourceMethodName = "Method,WithComma",
                        SourceMethodSignature = "void Method()",
                        Provider = AiProvider.OpenAi,
                        GenerationApproach = TestGenerationApproach.MetricsDriven,
                        ContextMode = GenerationContextMode.NoHistory,
                        BudgetMode = GenerationBudgetMode.PassAt1,
                        RunDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
                        GeneratedTestCompiled = true,
                        GeneratedTestExecuted = true,
                        GeneratedTestPassed = true
                    }
                ]);

            var text = await File.ReadAllTextAsync(path);

            Assert.Contains("experiment_run_id,repo_url,repo_owner", text);
            Assert.DoesNotContain("metrics_path", text);
            Assert.Contains("source_method_mi,source_method_cc,source_method_coupling,source_method_dit,source_method_sloc,source_method_eloc", text);
            Assert.Contains("baseline_test_mi,baseline_test_cc,baseline_test_coupling,baseline_test_dit,baseline_test_sloc,baseline_test_eloc", text);
            Assert.Contains("generated_test_mi,generated_test_cc,generated_test_coupling,generated_test_dit,generated_test_sloc,generated_test_eloc", text);
            Assert.Contains("roslyn_diagnostics_before_raw_count,roslyn_diagnostics_after_raw_count,new_actionable_roslyn_diagnostics_count", text);
            Assert.Contains("tool_observed_outcome", text);
            Assert.DoesNotContain(",classification,", text);
            Assert.Contains("\"Method,WithComma\"", text);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AppendAsync_AddsHeaderOnlyOnce()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-results-{Guid.NewGuid():N}.csv");
        var writer = new ExperimentResultsWriter();

        try
        {
            var run = new ExperimentRun { Id = 7, ResultsFilePath = path };
            var row = new ExperimentResultFileRow
            {
                ExperimentRunId = 7,
                Provider = AiProvider.OpenAi,
                GenerationApproach = TestGenerationApproach.Naive,
                ContextMode = GenerationContextMode.ChainedHistory,
                BudgetMode = GenerationBudgetMode.PassAt1,
                RunDate = DateTime.UtcNow
            };

            await writer.AppendAsync(run, row);
            await writer.AppendAsync(run, row);

            var lines = await File.ReadAllLinesAsync(path);

            Assert.Equal(3, lines.Length);
            Assert.Single(lines, x => x.StartsWith("experiment_run_id", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
