using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Rules;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Rules;
using TestMap.Rules.ProjectDiscovery;

namespace TestMap.UnitTests.Persistence;

public sealed class RuleAuditRepositoryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReplaceProjectDecisionsAsync_PersistsRuleDefinitionsAndCurrentDecisions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new RuleAuditRepository(db);
        await repository.UpsertRuleDefinitionsAsync(ProjectDiscoveryRuleDefinitions.All);
        await repository.ReplaceProjectDecisionsAsync(
            projectId: 12,
            cSharpProjectId: 34,
            [
                new RuleDecisionRecord
                {
                    DecisionKind = "WindowsRequirement",
                    Value = "Required",
                    RuleId = ProjectDiscoveryRuleDefinitions.WindowsRequiredByDesktop.Id,
                    RuleVersion = ProjectDiscoveryRuleDefinitions.WindowsRequiredByDesktop.Version,
                    Confidence = RuleConfidence.High,
                    Evidence =
                    [
                        new RuleEvidenceRecord
                        {
                            Source = "ProjectXml",
                            Key = "UseWPF",
                            Value = "true",
                            Location = "App.csproj"
                        }
                    ],
                    Notes = "Windows desktop project."
                }
            ]);

        await repository.ReplaceProjectDecisionsAsync(
            projectId: 12,
            cSharpProjectId: 34,
            [
                new RuleDecisionRecord
                {
                    DecisionKind = "WindowsRequirement",
                    Value = "NotRequired",
                    RuleId = ProjectDiscoveryRuleDefinitions.WindowsNotRequiredNoSignal.Id,
                    RuleVersion = ProjectDiscoveryRuleDefinitions.WindowsNotRequiredNoSignal.Version,
                    Confidence = RuleConfidence.Low,
                    Notes = "No Windows signal."
                }
            ]);

        var definitions = await db.RuleDefinitions.ToListAsync();
        var decisions = await db.RuleDecisions.ToListAsync();

        Assert.Contains(definitions, x =>
            x.RuleId == ProjectDiscoveryRuleDefinitions.WindowsRequiredByDesktop.Id &&
            x.RuleVersion == "1.0");
        var decision = Assert.Single(decisions);
        Assert.Equal(12, decision.ProjectId);
        Assert.Equal(34, decision.CSharpProjectId);
        Assert.Equal(ProjectDiscoveryRuleDefinitions.WindowsNotRequiredNoSignal.Id, decision.RuleId);
        Assert.Equal(RuleConfidence.Low, decision.Confidence);
    }
}
