using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.AgentTools;
using TestMap.Models.Experiment;
using TestMap.Models.Testing;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.AgentTools;
using TestMap.Services.Experiment.Execution;
using TestMap.Services.TestExecution;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="ToolPostAttemptMeasurementService"/> and its static
/// <see cref="ToolPostAttemptMeasurementService.Classify"/> method.
/// </summary>
public sealed class ToolPostAttemptMeasurementServiceTests
{
    // ─── Classify (static / pure) ─────────────────────────────────────────────

    /// <summary>
    /// All tests pass and mutation score is positive → ValidatedEvidencePositive.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_AllTestsPassMutationPositive_ReturnsValidatedEvidencePositive()
    {
        var run = new TestRunModel { Success = true, MutationScore = 0.12, Coverage = 0 };

        var (validation, observed) = ToolPostAttemptMeasurementService.Classify(run);

        Assert.Equal(ToolValidationOutcome.Passed, validation);
        Assert.Equal(ToolObservedOutcome.ValidatedEvidencePositive, observed);
    }

    /// <summary>
    /// All tests pass and coverage is positive → ValidatedEvidencePositive.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_AllTestsPassCoveragePositive_ReturnsValidatedEvidencePositive()
    {
        var run = new TestRunModel { Success = true, MutationScore = 0, Coverage = 80 };

        var (validation, observed) = ToolPostAttemptMeasurementService.Classify(run);

        Assert.Equal(ToolValidationOutcome.Passed, validation);
        Assert.Equal(ToolObservedOutcome.ValidatedEvidencePositive, observed);
    }

    /// <summary>
    /// All tests pass but no mutation or coverage improvement → ValidatedLowImpact.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_AllTestsPassNoImprovement_ReturnsValidatedLowImpact()
    {
        var run = new TestRunModel { Success = true, MutationScore = 0, Coverage = 0 };

        var (validation, observed) = ToolPostAttemptMeasurementService.Classify(run);

        Assert.Equal(ToolValidationOutcome.Passed, validation);
        Assert.Equal(ToolObservedOutcome.ValidatedLowImpact, observed);
    }

    /// <summary>
    /// Tests failed → ValidationFailed with TestsFailed outcome.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_TestsFailed_ReturnsValidationFailed()
    {
        var run = new TestRunModel
        {
            Success = false,
            Results = [new TestResultModel { Outcome = "Failed" }]
        };

        var (validation, observed) = ToolPostAttemptMeasurementService.Classify(run);

        Assert.Equal(ToolValidationOutcome.TestsFailed, validation);
        Assert.Equal(ToolObservedOutcome.ValidationFailed, observed);
    }

    /// <summary>
    /// Build failed (no test results) → ValidationFailed with BuildFailed outcome.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_BuildFailed_ReturnsBuildFailed()
    {
        var run = new TestRunModel { Success = false, Results = [] };

        var (validation, observed) = ToolPostAttemptMeasurementService.Classify(run);

        Assert.Equal(ToolValidationOutcome.BuildFailed, validation);
        Assert.Equal(ToolObservedOutcome.ValidationFailed, observed);
    }

    // ─── MeasureAsync ────────────────────────────────────────────────────────

    /// <summary>
    /// Non-Completed attempt (e.g., TimedOut) is skipped without calling BuildTestAsync.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task MeasureAsync_NonCompletedStatus_ReturnsSkipped()
    {
        var mock = new MockBuildTestService();
        await using var db = await CreateInMemoryDbAsync();
        var repo = new ToolAttemptRepository(db);
        var service = new ToolPostAttemptMeasurementService(mock, repo);
        var attempt = new ToolAttempt { RunStatus = ToolRunStatus.TimedOut };

        var result = await service.MeasureAsync(
            attempt,
            new CandidateMethod { MethodName = "Foo" },
            MakeMethodContext());

        Assert.False(result.Measured);
        Assert.Equal(0, mock.CallCount);
    }

    /// <summary>
    /// Completed attempt with empty TestProjectPath is skipped.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task MeasureAsync_EmptyTestProjectPath_ReturnsSkipped()
    {
        var mock = new MockBuildTestService();
        await using var db = await CreateInMemoryDbAsync();
        var service = new ToolPostAttemptMeasurementService(mock, new ToolAttemptRepository(db));
        var attempt = new ToolAttempt { RunStatus = ToolRunStatus.Completed };

        var result = await service.MeasureAsync(
            attempt,
            new CandidateMethod { MethodName = "Foo" },
            MakeMethodContext(testProjectPath: string.Empty));

        Assert.False(result.Measured);
        Assert.Equal(0, mock.CallCount);
    }

    /// <summary>
    /// Happy path: measurement runs, updates attempt outcomes, and returns Measured=true.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task MeasureAsync_SuccessfulRun_UpdatesAttemptAndReturnsMeasured()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var (runId, candidateId) = await SeedExperimentGraphAsync(db);
        var repo = new ToolAttemptRepository(db);
        var attempt = new ToolAttempt
        {
            ExperimentRunId = runId,
            CandidateMethodId = candidateId,
            ToolId = "codex",
            RunStatus = ToolRunStatus.Completed,
            StartedAt = DateTime.UtcNow,
            TimeoutSeconds = 2700
        };
        attempt.Id = await repo.InsertAsync(attempt);

        var mock = new MockBuildTestService(new TestRunModel { DbId = 7, Success = true, MutationScore = 0.1 });
        var service = new ToolPostAttemptMeasurementService(mock, repo);

        // Act
        var result = await service.MeasureAsync(
            attempt,
            new CandidateMethod { MethodName = "Calculate" },
            MakeMethodContext());

        // Assert
        Assert.True(result.Measured);
        Assert.Equal(7, result.TestRunId);
        Assert.Equal(ToolValidationOutcome.Passed, result.ValidationOutcome);
        Assert.Equal(ToolObservedOutcome.ValidatedEvidencePositive, result.ObservedOutcome);

        // Verify attempt was updated in DB.
        db.ChangeTracker.Clear();
        var updated = await repo.GetByIdAsync(attempt.Id);
        Assert.Equal(7, updated!.PostAttemptTestRunId);
        Assert.Equal(ToolObservedOutcome.ValidatedEvidencePositive, updated.ObservedOutcome);
        Assert.Equal(ToolValidationOutcome.Passed, updated.ValidationOutcome);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateInMemoryDbAsync()
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>()
                .UseSqlite(new SqliteConnection("Data Source=:memory:"))
                .Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<(int ExperimentRunId, int CandidateMethodId)>
        SeedExperimentGraphAsync(TestMapDbContext db)
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
            SourceMemberId = 10,
            SourceMethodName = "Calculate",
            SourceMethodSignature = "public int Calculate()"
        };
        db.CandidateMethods.Add(candidate);
        await db.SaveChangesAsync();

        return (run.Id, candidate.Id);
    }

    private static CandidateMethodContext MakeMethodContext(
        string testProjectPath = "/repo/Tests",
        string sourceProjectPath = "/repo/Src") =>
        new()
        {
            Method = new CandidateMethod { Id = 1, MemberId = 1, MethodName = "Foo" },
            MethodSignature = "void Foo()",
            ContainingClass = "MyClass",
            TestNamespace = "Tests",
            TestClassName = "MyClassTests",
            TestFilePath = "/repo/Tests/MyClassTests.cs",
            SourceFilePath = "/repo/Src/MyClass.cs",
            SourceLocation = new CandidateSourceLocation(),
            SourceProjectPath = sourceProjectPath,
            TestProjectPath = testProjectPath,
            TargetBuildFramework = "net10.0",
            SolutionFilePath = string.Empty,
            ExampleTest = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = string.Empty,
            TestFileContents = string.Empty,
            TestSupportContext = string.Empty,
            TestFramework = "xunit",
            TestDependencies = string.Empty,
            CoverageGapSummary = string.Empty
        };

    private sealed class MockBuildTestService : IBuildTestService
    {
        private readonly TestRunModel _response;
        public int CallCount { get; private set; }

        public MockBuildTestService(TestRunModel? response = null)
        {
            _response = response ?? new TestRunModel { DbId = 1, Success = true };
        }

        public Task<TestRunModel> BuildTestAsync(BuildTestRunRequest request)
        {
            CallCount++;
            return Task.FromResult(_response);
        }
    }
}
