using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.AgentTools;
using TestMap.Models.Experiment.Assertions;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.AgentTools;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.AgentTools;
using TestMap.Persistence.Ef.Repositories.Experiment.Assertions;
using TestMap.Services.Experiment.Reporting;
using TestMap.Services.StaticAnalysis.Assertions;

namespace TestMap.UnitTests.AgentTools;

public sealed class ToolAttemptAssertionLineageTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task PostLinkAnalysis_PersistsPerTestOwnersAndAggregatesMultipleTests()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId, toolAttemptId) = await SeedToolAttemptAsync(db);
        var children = new[]
        {
            new ToolAttemptGeneratedTestEntity { ToolAttemptId = toolAttemptId, MemberId = 101 },
            new ToolAttemptGeneratedTestEntity { ToolAttemptId = toolAttemptId, MemberId = 102 }
        };
        db.ToolAttemptGeneratedTests.AddRange(children);
        await db.SaveChangesAsync();
        var measurement = MakeMeasurement(runId, candidateId, toolAttemptId);
        measurement.TestSummaries.Add(
            MakeSummary(101, children[0].Id, AssertionLineageCategory.Traced));
        measurement.TestSummaries.Add(
            MakeSummary(102, children[1].Id, AssertionLineageCategory.Trivial));
        var repository = MakeRepository(db);

        var id = await repository.InsertAsync(measurement);
        db.ChangeTracker.Clear();
        var persisted = await repository.GetByIdAsync(id);

        Assert.NotNull(persisted);
        Assert.Equal(AssertionMeasurementStatus.Complete, persisted!.Status);
        Assert.Equal(2, persisted.EligibleTestCount);
        Assert.Equal(2, persisted.AnalyzedTestCount);
        Assert.Equal(2, persisted.RecognizedAssertionCount);
        Assert.Equal(1, persisted.TracedAssertionCount);
        Assert.Equal(1, persisted.TrivialAssertionCount);
        Assert.Equal(
            children.Select(x => x.Id).Order(),
            persisted.TestSummaries.Select(x => x.ToolAttemptGeneratedTestId!.Value).Order());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task NoLinkedGeneratedTests_PersistsUnavailableWithoutInventedZeroCounts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId, toolAttemptId) = await SeedToolAttemptAsync(db);
        var measurement = MakeMeasurement(runId, candidateId, toolAttemptId);
        measurement.Status = AssertionMeasurementStatus.Unavailable;
        measurement.FailureCode = AssertionLineageReasonCodes.GeneratedTestMemberUnresolved;
        measurement.FailureReason =
            "No generated or modified test member could be attributed to the tool attempt.";

        var id = await MakeRepository(db).InsertAsync(measurement);
        db.ChangeTracker.Clear();
        var persisted = await MakeRepository(db).GetByIdAsync(id);

        Assert.NotNull(persisted);
        Assert.Equal(AssertionMeasurementStatus.Unavailable, persisted!.Status);
        Assert.Empty(persisted.TestSummaries);
        Assert.Null(persisted.EligibleTestCount);
        Assert.Null(persisted.AnalyzedTestCount);
        Assert.Null(persisted.RecognizedAssertionCount);
        Assert.Null(persisted.TracedAssertionCount);
    }

    private static AssertionLineageMeasurementRepository MakeRepository(TestMapDbContext db) =>
        new(db, new AssertionLineageSummaryService(), new AssertionLineageAuditService());

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<(int RunId, int CandidateId, int ToolAttemptId)> SeedToolAttemptAsync(
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
            SourceMemberId = 11,
            SourceMethodName = "Calculate",
            SourceMethodSignature = "int Calculate()"
        };
        db.CandidateMethods.Add(candidate);
        await db.SaveChangesAsync();
        var attempt = new ToolAttempt
        {
            ExperimentRunId = run.Id,
            CandidateMethodId = candidate.Id,
            ToolId = "codex",
            RunStatus = ToolRunStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            TimeoutSeconds = 60
        };
        attempt.Id = await new ToolAttemptRepository(db).InsertAsync(attempt);
        return (run.Id, candidate.Id, attempt.Id);
    }

    private static AssertionLineageMeasurement MakeMeasurement(
        int runId,
        int candidateId,
        int toolAttemptId) => new()
    {
        ProjectId = 1,
        ExperimentRunId = runId,
        CandidateMethodId = candidateId,
        ToolAttemptId = toolAttemptId,
        ProducerLane = "agent-tool",
        Status = AssertionMeasurementStatus.Complete,
        PolicyVersion = AssertionLineagePolicy.CurrentPolicyVersion,
        AssertionCatalogVersion = AssertionLineagePolicy.CurrentCatalogVersion,
        MaxDepth = 4,
        StartedAt = DateTime.UtcNow,
        CompletedAt = DateTime.UtcNow
    };

    private static GeneratedTestAssertionSummary MakeSummary(
        int memberId,
        int childId,
        AssertionLineageCategory category)
    {
        var traced = category == AssertionLineageCategory.Traced;
        return new GeneratedTestAssertionSummary
        {
            TestMemberId = memberId,
            ToolAttemptGeneratedTestId = childId,
            TestMethodName = $"Generated_{memberId}",
            TestFilePath = "WidgetTests.cs",
            TestMemberContentHash = $"{memberId:x64}",
            FallbackIdentityHash = $"{childId:x64}",
            Observations =
            [
                new AssertionObservation
                {
                    Ordinal = 0,
                    Framework = "xunit",
                    AssertionMethod = traced ? "Equal" : "True",
                    RecognitionKind = AssertionRecognitionKind.Semantic,
                    FilePath = "WidgetTests.cs",
                    StartLine = memberId,
                    StartColumn = 0,
                    EndLine = memberId,
                    EndColumn = 20,
                    ExpressionHash = $"{memberId + 1000:x64}",
                    Category = category,
                    ResolutionCode = traced
                        ? AssertionLineageReasonCodes.ProductionInvocation
                        : AssertionLineageReasonCodes.AllInputsTestLocal,
                    DepthReached = 1,
                    TargetRelation = traced
                        ? AssertionTargetRelation.Candidate
                        : AssertionTargetRelation.NoProduction,
                    TraceSummary = traced
                        ? "Assertion input reaches the intended production method."
                        : "Assertion input terminates in test-local literals.",
                    Steps =
                    [
                        new AssertionLineageStep
                        {
                            InputIndex = 0,
                            PathIndex = 0,
                            StepIndex = 0,
                            StepKind = traced
                                ? AssertionLineageStepKind.ProductionMember
                                : AssertionLineageStepKind.Literal,
                            Depth = 1,
                            SymbolDisplay = traced ? "Widget.Calculate()" : "true",
                            MemberId = traced ? 11 : null,
                            Outcome = traced
                                ? AssertionLineageStepOutcome.Traced
                                : AssertionLineageStepOutcome.Trivial,
                            ReasonCode = traced
                                ? AssertionLineageReasonCodes.ProductionInvocation
                                : AssertionLineageReasonCodes.AllInputsTestLocal,
                            Summary = "Terminal assertion lineage step."
                        }
                    ]
                }
            ]
        };
    }
}
