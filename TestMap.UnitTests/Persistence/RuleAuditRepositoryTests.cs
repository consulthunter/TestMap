using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Rules;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
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

    /// <summary>
    /// Calling UpsertRuleDefinitionsAsync with the same RuleId+Version a second time updates the
    /// name/description in place — no duplicate row is created.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpsertRuleDefinitionsAsync_SameIdAndVersion_UpdatesInPlaceWithoutDuplicating()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new RuleAuditRepository(db);
        var originalDef = ProjectDiscoveryRuleDefinitions.WindowsRequiredByDesktop;

        // First upsert — inserts the row
        await repository.UpsertRuleDefinitionsAsync([originalDef]);
        Assert.Equal(1, await db.RuleDefinitions.CountAsync());

        // Second upsert with modified name — same id + version
        var updatedDef = new RuleDefinition
        {
            Id = originalDef.Id,
            Version = originalDef.Version,
            Name = "Updated Display Name",
            Description = "Updated description.",
            Category = originalDef.Category
        };
        await repository.UpsertRuleDefinitionsAsync([updatedDef]);

        // Still only one row, and the name was updated
        var all = await db.RuleDefinitions.ToListAsync();
        var single = Assert.Single(all);
        Assert.Equal(originalDef.Id, single.RuleId);
        Assert.Equal("Updated Display Name", single.Name);
        Assert.Equal("Updated description.", single.Description);
    }

    /// <summary>
    /// Upserting the same RuleId at a different version creates a new row — versions are
    /// tracked independently so history is preserved.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpsertRuleDefinitionsAsync_SameIdDifferentVersion_CreatesNewRow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new RuleAuditRepository(db);
        var baseId = ProjectDiscoveryRuleDefinitions.WindowsRequiredByDesktop.Id;

        var v1 = new RuleDefinition { Id = baseId, Version = "1.0", Name = "Rule v1", Category = "Test" };
        var v2 = new RuleDefinition { Id = baseId, Version = "2.0", Name = "Rule v2", Category = "Test" };

        await repository.UpsertRuleDefinitionsAsync([v1]);
        await repository.UpsertRuleDefinitionsAsync([v2]);

        var rows = await db.RuleDefinitions.Where(x => x.RuleId == baseId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, x => x.RuleVersion == "1.0" && x.Name == "Rule v1");
        Assert.Contains(rows, x => x.RuleVersion == "2.0" && x.Name == "Rule v2");
    }

    /// <summary>
    /// AddScopedDecisionsAsync persists all scope fields (ScopeKind, ScopeId) and all optional
    /// experiment FK fields (ExperimentRunId, CandidateMethodId, GenerationAttemptId, TestExecutionId).
    /// Parent records are seeded to satisfy FK constraints enforced by SQLite.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AddScopedDecisionsAsync_PersistsAllScopeAndForeignKeyFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();

        // Seed the experiment hierarchy so FK constraints are satisfied
        db.ExperimentRuns.Add(new ExperimentRunEntity
        {
            Id = 3, ProjectId = 7, StartTime = DateTime.UtcNow,
            Objective = "TestSuiteExpansion", CandidateSelectionStrategy = "MetricsDriven",
            Configuration = "{}", ResultsFilePath = string.Empty, CandidateLimit = 1, Status = "Running"
        });
        db.CandidateMethods.Add(new CandidateMethodEntity
        {
            Id = 5, ExperimentRunId = 3, SourceMemberId = 10,
            SourceMethodName = "Work", SourceMethodSignature = "Work()", SelectionTime = DateTime.UtcNow
        });
        db.GenerationAttempts.Add(new GenerationAttemptEntity
        {
            Id = 11, CandidateMethodId = 5, ProviderName = "Provider", ModelName = "Model",
            Strategy = "Generate", Objective = "TestSuiteExpansion", GenerationApproach = "MetricsDriven",
            StartTime = DateTime.UtcNow, Status = "Completed"
        });
        db.TestExecutions.Add(new GeneratedTestExecutionEntity
        {
            Id = 13, GenerationAttemptId = 11, GeneratedTestMethodName = "Test_Work",
            ExecutionTime = DateTime.UtcNow, TestClassification = "Accepted"
        });
        await db.SaveChangesAsync();

        var repository = new RuleAuditRepository(db);
        await repository.UpsertRuleDefinitionsAsync(ProjectDiscoveryRuleDefinitions.All);

        await repository.AddScopedDecisionsAsync(
            projectId: 7,
            scopeKind: "CandidateMethod",
            scopeId: "5",
            decisions:
            [
                new RuleDecisionRecord
                {
                    DecisionKind = "WindowsRequirement",
                    Value = "Required",
                    RuleId = ProjectDiscoveryRuleDefinitions.WindowsRequiredByDesktop.Id,
                    RuleVersion = ProjectDiscoveryRuleDefinitions.WindowsRequiredByDesktop.Version,
                    Confidence = RuleConfidence.High,
                    Notes = "Scoped decision."
                }
            ],
            experimentRunId: 3,
            candidateMethodId: 5,
            generationAttemptId: 11,
            testExecutionId: 13);

        var persisted = Assert.Single(await db.RuleDecisions.ToListAsync());
        Assert.Equal(7, persisted.ProjectId);
        Assert.Equal("CandidateMethod", persisted.ScopeKind);
        Assert.Equal("5", persisted.ScopeId);
        Assert.Equal(3, persisted.ExperimentRunId);
        Assert.Equal(5, persisted.CandidateMethodId);
        Assert.Equal(11, persisted.GenerationAttemptId);
        Assert.Equal(13, persisted.GeneratedTestExecutionId);
        Assert.Equal(RuleConfidence.High, persisted.Confidence);
    }

    /// <summary>
    /// ReplaceProjectDecisionsAsync only removes decisions for the specified project+csproject;
    /// decisions belonging to other projects are left untouched.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReplaceProjectDecisionsAsync_IsolatesDecisionsByProject()
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

        var decision = new RuleDecisionRecord
        {
            DecisionKind = "WindowsRequirement",
            Value = "Required",
            RuleId = ProjectDiscoveryRuleDefinitions.WindowsRequiredByDesktop.Id,
            RuleVersion = ProjectDiscoveryRuleDefinitions.WindowsRequiredByDesktop.Version,
            Confidence = RuleConfidence.High
        };

        // Seed decisions for two different projects
        await repository.ReplaceProjectDecisionsAsync(projectId: 10, cSharpProjectId: 100, [decision]);
        await repository.ReplaceProjectDecisionsAsync(projectId: 20, cSharpProjectId: 200, [decision]);
        Assert.Equal(2, await db.RuleDecisions.CountAsync());

        // Replace project 10's decisions — project 20 must survive
        await repository.ReplaceProjectDecisionsAsync(projectId: 10, cSharpProjectId: 100, [decision]);

        Assert.Equal(2, await db.RuleDecisions.CountAsync());
        Assert.Equal(1, await db.RuleDecisions.CountAsync(x => x.ProjectId == 10));
        Assert.Equal(1, await db.RuleDecisions.CountAsync(x => x.ProjectId == 20));
    }
}
