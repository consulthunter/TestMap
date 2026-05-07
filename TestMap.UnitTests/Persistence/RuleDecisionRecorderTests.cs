using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Rules;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Rules;
using TestMap.Rules.Generation;
using TestMap.Services.Rules;

namespace TestMap.UnitTests.Persistence;

public sealed class RuleDecisionRecorderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordAsync_PersistsScopedDecisionsAndDefinitions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var recorder = new RuleDecisionRecorder(new RuleAuditRepository(db));
        var decision = new RuleDecisionRecord
        {
            DecisionKind = "GenerationValidation",
            Value = "CoverageImproved",
            RuleId = GenerationValidationRuleDefinitions.CoverageImproved.Id,
            RuleVersion = GenerationValidationRuleDefinitions.CoverageImproved.Version,
            Confidence = RuleConfidence.High,
            Evidence =
            [
                new RuleEvidenceRecord
                {
                    Source = "Validation",
                    Key = "CoverageImprovement",
                    Value = "0.25"
                }
            ],
            Notes = "Generated test improved coverage."
        };

        await recorder.RecordAsync(
            projectId: 7,
            scope: RuleDecisionScope.TestExecution(13),
            decisions: [decision],
            experimentRunId: 3,
            candidateMethodId: 5,
            generationAttemptId: 11,
            testExecutionId: 13);

        var persisted = Assert.Single(await db.RuleDecisions.ToListAsync());
        Assert.Equal("TestExecution", persisted.ScopeKind);
        Assert.Equal("13", persisted.ScopeId);
        Assert.Equal(3, persisted.ExperimentRunId);
        Assert.Equal(5, persisted.CandidateMethodId);
        Assert.Equal(11, persisted.GenerationAttemptId);
        Assert.Equal(13, persisted.TestExecutionId);
        Assert.Equal(GenerationValidationRuleDefinitions.CoverageImproved.Id, persisted.RuleId);
        Assert.Single(persisted.Evidence);
        Assert.Contains(await db.RuleDefinitions.ToListAsync(), x =>
            x.RuleId == GenerationValidationRuleDefinitions.CoverageImproved.Id &&
            x.RuleVersion == GenerationValidationRuleDefinitions.CoverageImproved.Version);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CreateSnapshotJson_WritesEmbeddedDecisionSnapshot()
    {
        var recorder = new RuleDecisionRecorder(new RuleAuditRepository(
            new TestMapDbContext(new DbContextOptionsBuilder<TestMapDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options)));

        var json = recorder.CreateSnapshotJson(
        [
            new RuleDecisionRecord
            {
                DecisionKind = "GenerationValidation",
                Value = "CoverageImproved",
                RuleId = GenerationValidationRuleDefinitions.CoverageImproved.Id,
                RuleVersion = "1.0"
            }
        ]);

        Assert.Contains("CoverageImproved", json, StringComparison.Ordinal);
        Assert.Contains(GenerationValidationRuleDefinitions.CoverageImproved.Id, json, StringComparison.Ordinal);
    }
}
