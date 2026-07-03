using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Entities.MutationTesting;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.TargetSelection.Strategies;

namespace TestMap.UnitTests.TestGeneration;

public sealed class MetricDrivenCandidateSelectionStrategyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_CountsUndetectedMutantsWithSqliteTranslatablePredicate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.MutationTestingReports.Add(new MutationTestingReportEntity
        {
            Id = 1,
            ProjectId = 1,
            SchemaVersion = "test",
            ProjectRoot = "."
        });
        db.Mutants.AddRange(
            new MutantEntity
            {
                MutationTestingReportId = 1,
                MemberId = 1,
                StrykerMutantId = "M1",
                Status = "Survived",
                ContentHash = "hash-m1"
            },
            new MutantEntity
            {
                MutationTestingReportId = 1,
                MemberId = 2,
                StrykerMutantId = "M2",
                Status = "Killed",
                ContentHash = "hash-m2"
            });
        await db.SaveChangesAsync();

        var strategy = new MetricDrivenCandidateSelectionStrategy(db);
        var selected = await strategy.SelectAsync(
            new CandidateSelectionContext
            {
                ExperimentConfiguration = new ExperimentConfig(),
                TargetSelection = new TargetSelectionConfig
                {
                    MetricDrivenImprovement = new MetricDrivenImprovementConfig
                    {
                        Metric = MetricDrivenMetric.SurvivingMutants,
                        Budget = new MetricDrivenBudgetConfig { MaxTargets = 1 }
                    }
                },
                SelectionTime = DateTime.UtcNow,
                EffectiveLimit = 1
            },
            [
                new CandidateSelectionRow(1, "SurvivingTarget", "public void SurvivingTarget() { }", 0.0, 1),
                new CandidateSelectionRow(2, "KilledTarget", "public void KilledTarget() { }", 0.0, 1)
            ]);

        Assert.Equal(2, selected.Count);
        var candidate = selected.First();
        Assert.Equal(1, candidate.MemberId);
        Assert.Contains("undetected_mutants=1", candidate.MetricSelectionReason);
    }

    /// <summary>
    /// A candidate with LineCoverage metric and uncovered lines scores higher than a
    /// fully-covered candidate, so the uncovered one appears first in the output.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_LineCoverageMetric_RanksUncoveredMemberHigher()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.MemberCoverages.AddRange(
            // Member 1: 0/10 lines covered — high expected delta
            new MemberCoverageEntity
            {
                Id = 1, MemberId = 1, CoverageReportId = 1, LineRate = 0.0,
                LinesValid = 10, LinesCovered = 0, Complexity = 1
            },
            // Member 2: 10/10 lines covered — zero expected delta
            new MemberCoverageEntity
            {
                Id = 2, MemberId = 2, CoverageReportId = 1, LineRate = 1.0,
                LinesValid = 10, LinesCovered = 10, Complexity = 1
            });
        await db.SaveChangesAsync();

        var strategy = new MetricDrivenCandidateSelectionStrategy(db);
        var selected = await strategy.SelectAsync(
            new CandidateSelectionContext
            {
                ExperimentConfiguration = new Models.Configuration.ExperimentConfig(),
                TargetSelection = new TargetSelectionConfig
                {
                    MetricDrivenImprovement = new MetricDrivenImprovementConfig
                    {
                        Metric = MetricDrivenMetric.LineCoverage
                    }
                },
                SelectionTime = DateTime.UtcNow,
                EffectiveLimit = 2
            },
            [
                new CandidateSelectionRow(1, "UncoveredMethod", "public void UncoveredMethod() { }", 0.0, 1),
                new CandidateSelectionRow(2, "CoveredMethod", "public void CoveredMethod() { }", 1.0, 1)
            ]);

        Assert.Equal(2, selected.Count);
        Assert.Equal(1, selected[0].MemberId); // uncovered method ranked first
    }

    /// <summary>
    /// A candidate whose expected metric delta is below 0.01 receives guardrail
    /// status "failed". When ExcludeFailedGuardrails is true, its final score is forced
    /// to zero and it appears last in the ordered output.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_GuardrailFailed_ScoreSetToZeroAndCandidateRankedLast()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.MutationTestingReports.Add(new MutationTestingReportEntity
        {
            Id = 1, ProjectId = 1, SchemaVersion = "test", ProjectRoot = "."
        });
        // Member 1: has surviving mutants → positive delta
        db.Mutants.Add(new MutantEntity
        {
            MutationTestingReportId = 1, MemberId = 1, StrykerMutantId = "M1",
            Status = "Survived", ContentHash = "h1"
        });
        // Member 2: no surviving mutants → delta below 0.01 → guardrail fails
        await db.SaveChangesAsync();

        var strategy = new MetricDrivenCandidateSelectionStrategy(db);
        var selected = await strategy.SelectAsync(
            new CandidateSelectionContext
            {
                ExperimentConfiguration = new Models.Configuration.ExperimentConfig(),
                TargetSelection = new TargetSelectionConfig
                {
                    MetricDrivenImprovement = new MetricDrivenImprovementConfig
                    {
                        Metric = MetricDrivenMetric.SurvivingMutants,
                        Guardrails = new MetricDrivenGuardrailsConfig
                        {
                            ExcludeFailedGuardrails = true
                        }
                    }
                },
                SelectionTime = DateTime.UtcNow,
                EffectiveLimit = 2
            },
            [
                new CandidateSelectionRow(1, "WithMutants", "public void WithMutants() { }", 0.0, 1),
                new CandidateSelectionRow(2, "WithoutMutants", "public void WithoutMutants() { }", 0.0, 1)
            ]);

        Assert.Equal(2, selected.Count);
        Assert.Equal(1, selected[0].MemberId);              // positive delta first
        Assert.Equal("failed", selected[1].MetricGuardrailStatus);
    }

    /// <summary>
    /// Weight validation throws when any individual weight falls outside [0.01, 0.99].
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_InvalidWeightOutOfRange_ThrowsInvalidOperationException()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var strategy = new MetricDrivenCandidateSelectionStrategy(db);
        var badConfig = new MetricDrivenImprovementConfig
        {
            Metric = MetricDrivenMetric.SurvivingMutants,
            Weights = new MetricDrivenWeightsConfig
            {
                ExpectedMetricDelta = 0.0,  // below minimum 0.01
                Confidence = 0.25,
                Feasibility = 0.25,
                InverseCost = 0.25,
                Guardrail = 0.25
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.SelectAsync(
                new CandidateSelectionContext
                {
                    ExperimentConfiguration = new Models.Configuration.ExperimentConfig(),
                    TargetSelection = new TargetSelectionConfig { MetricDrivenImprovement = badConfig },
                    SelectionTime = DateTime.UtcNow,
                    EffectiveLimit = 1
                },
                [new CandidateSelectionRow(1, "M", "public void M() { }", 0.0, 1)]));
    }

    /// <summary>
    /// Weight validation throws when the weights do not sum to 1.0 (within tolerance).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_WeightsNotSummingToOne_ThrowsInvalidOperationException()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var strategy = new MetricDrivenCandidateSelectionStrategy(db);
        var badConfig = new MetricDrivenImprovementConfig
        {
            Metric = MetricDrivenMetric.SurvivingMutants,
            Weights = new MetricDrivenWeightsConfig
            {
                ExpectedMetricDelta = 0.5,  // sum = 0.5+0.2+0.2+0.2+0.2 = 1.3 ≠ 1.0
                Confidence = 0.2,
                Feasibility = 0.2,
                InverseCost = 0.2,
                Guardrail = 0.2
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.SelectAsync(
                new CandidateSelectionContext
                {
                    ExperimentConfiguration = new Models.Configuration.ExperimentConfig(),
                    TargetSelection = new TargetSelectionConfig { MetricDrivenImprovement = badConfig },
                    SelectionTime = DateTime.UtcNow,
                    EffectiveLimit = 1
                },
                [new CandidateSelectionRow(1, "M", "public void M() { }", 0.0, 1)]));
    }

    /// <summary>
    /// When the candidate pool is empty, SelectAsync returns an empty list without
    /// performing any database queries that could throw.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_EmptyPool_ReturnsEmptyList()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var strategy = new MetricDrivenCandidateSelectionStrategy(db);
        var selected = await strategy.SelectAsync(
            new CandidateSelectionContext
            {
                ExperimentConfiguration = new Models.Configuration.ExperimentConfig(),
                TargetSelection = new TargetSelectionConfig
                {
                    MetricDrivenImprovement = new MetricDrivenImprovementConfig
                        { Metric = MetricDrivenMetric.SurvivingMutants }
                },
                SelectionTime = DateTime.UtcNow,
                EffectiveLimit = 10
            },
            []);

        Assert.Empty(selected);
    }
}
