using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Persistence.Ef;
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
        db.Mutants.AddRange(
            new MutantEntity
            {
                MemberId = 1,
                StrykerMutantId = "M1",
                Status = "Survived",
                ContentHash = "hash-m1"
            },
            new MutantEntity
            {
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
}
