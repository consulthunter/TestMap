using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Experiment.Assertions;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Persistence.Ef.Repositories.Experiment.Assertions;
using TestMap.Services.Experiment.Reporting;

namespace TestMap.UnitTests.Persistence;

public sealed class AssertionLineageMeasurementRepositoryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_RoundTripsOrderedAssertionGraph()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId, attemptId) = await SeedGenerationAttemptAsync(db);
        var repository = MakeRepository(db);
        var measurement = MakeMeasurement(runId, candidateId, attemptId);

        var id = await repository.InsertAsync(measurement);
        db.ChangeTracker.Clear();
        var persisted = await repository.GetByIdAsync(id);

        Assert.NotNull(persisted);
        Assert.True(id > 0);
        Assert.True(measurement.TestSummaries[0].Id > 0);
        Assert.True(measurement.TestSummaries[0].Observations[0].Id > 0);
        Assert.True(measurement.TestSummaries[0].Observations[0].Steps[0].Id > 0);
        Assert.Equal(AssertionMeasurementStatus.Complete, persisted!.Status);
        Assert.Equal(AssertionLineageCategory.Traced, persisted.TestSummaries[0].Observations[0].Category);
        Assert.Equal(
            AssertionLineageStepOutcome.Traced,
            persisted.TestSummaries[0].Observations[0].Steps[0].Outcome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_RejectsDuplicateAttemptPolicyMeasurement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId, attemptId) = await SeedGenerationAttemptAsync(db);
        var repository = MakeRepository(db);

        await repository.InsertAsync(MakeMeasurement(runId, candidateId, attemptId));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.InsertAsync(MakeMeasurement(runId, candidateId, attemptId)));
        Assert.Contains("immutable", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetForGenerationAttemptOrNotMeasuredAsync_DerivesHistoricalState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (_, _, attemptId) = await SeedGenerationAttemptAsync(db);
        var repository = MakeRepository(db);

        var historical = await repository.GetForGenerationAttemptOrNotMeasuredAsync(
            attemptId,
            "assertion-lineage-v1",
            "assertion-catalog-v1",
            4);

        Assert.Equal(AssertionMeasurementStatus.NotMeasured, historical.Status);
        Assert.Equal(AssertionLineageReasonCodes.HistoricalNotMeasured, historical.FailureCode);
        Assert.Null(historical.RecognizedAssertionCount);
        Assert.Empty(historical.TestSummaries);
        Assert.Empty(await db.AssertionLineageMeasurements.ToListAsync());
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

    private static async Task<(int RunId, int CandidateId, int AttemptId)> SeedGenerationAttemptAsync(
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
            SourceMemberId = 10,
            SourceMethodName = "Calculate",
            SourceMethodSignature = "public int Calculate()"
        };
        db.CandidateMethods.Add(candidate);
        await db.SaveChangesAsync();

        var generationRepository = new GenerationAttemptRepository(db);
        var attemptId = await generationRepository.InsertAsync(new GenerationAttempt
        {
            CandidateMethodId = candidate.Id,
            Provider = AiProvider.OpenAi,
            ModelName = "gpt",
            BudgetMode = GenerationBudgetMode.PassAt1,
            Objective = TestGenerationObjective.TestSuiteExpansion,
            GenerationApproach = TestGenerationApproach.MetricsDriven,
            ContextMode = GenerationContextMode.ChainedHistory,
            StartedAt = DateTime.UtcNow,
            AttemptNumber = 1,
            Status = "Completed"
        });
        return (run.Id, candidate.Id, attemptId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrDegradeAsync_AuditRejectsEvidence_StoresUnavailableInsteadOfThrowing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId, attemptId) = await SeedGenerationAttemptAsync(db);
        var repository = MakeRepository(db);
        var measurement = MakeMeasurement(runId, candidateId, attemptId);

        // A step with no stable reason code is exactly the shape the audit rejects. Assertion
        // lineage is enrichment, so this must not cost the attempt its results.
        measurement.TestSummaries[0].Observations[0].Steps[0].ReasonCode = "not-a-stable-code";

        var result = await repository.InsertOrDegradeAsync(measurement);

        Assert.True(result.Degraded);
        Assert.Contains("UnknownStepReasonCode", result.AuditFindings);

        db.ChangeTracker.Clear();
        var persisted = await repository.GetByIdAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(AssertionMeasurementStatus.Unavailable, persisted!.Status);
        Assert.Equal(AssertionLineageReasonCodes.AssertionAuditFailed, persisted.FailureCode);
        Assert.Empty(persisted.TestSummaries);
        Assert.Null(persisted.RecognizedAssertionCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrDegradeAsync_ValidEvidence_StoresMeasurementUnchanged()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId, attemptId) = await SeedGenerationAttemptAsync(db);
        var repository = MakeRepository(db);

        var result = await repository.InsertOrDegradeAsync(
            MakeMeasurement(runId, candidateId, attemptId));

        Assert.False(result.Degraded);
        Assert.Null(result.AuditFindings);

        db.ChangeTracker.Clear();
        var persisted = await repository.GetByIdAsync(result.Id);
        Assert.Equal(AssertionMeasurementStatus.Complete, persisted!.Status);
        Assert.Single(persisted.TestSummaries);
    }

    private static AssertionLineageMeasurement MakeMeasurement(
        int runId,
        int candidateId,
        int attemptId) => new()
    {
        ProjectId = 1,
        ExperimentRunId = runId,
        CandidateMethodId = candidateId,
        GenerationAttemptId = attemptId,
        ProducerLane = "testmap",
        PolicyVersion = "assertion-lineage-v1",
        AssertionCatalogVersion = "assertion-catalog-v1",
        MaxDepth = 4,
        StartedAt = DateTime.UtcNow,
        CompletedAt = DateTime.UtcNow,
        TestSummaries =
        [
            new GeneratedTestAssertionSummary
            {
                TestMemberId = 101,
                TestMethodName = "Calculate_returns_value",
                TestFilePath = "WidgetTests.cs",
                TestMemberContentHash = new string('a', 64),
                FallbackIdentityHash = new string('b', 64),
                Observations =
                [
                    new AssertionObservation
                    {
                        Ordinal = 0,
                        Framework = "xunit",
                        AssertionMethod = "Equal",
                        RecognitionKind = AssertionRecognitionKind.Semantic,
                        FilePath = "WidgetTests.cs",
                        StartLine = 10,
                        StartColumn = 9,
                        EndLine = 10,
                        EndColumn = 34,
                        ExpressionHash = new string('c', 64),
                        Category = AssertionLineageCategory.Traced,
                        ResolutionCode = AssertionLineageReasonCodes.ProductionInvocation,
                        DepthReached = 1,
                        TargetRelation = AssertionTargetRelation.Candidate,
                        TraceSummary = "Assertion input reaches Widget.Calculate().",
                        Steps =
                        [
                            new AssertionLineageStep
                            {
                                InputIndex = 0,
                                PathIndex = 0,
                                StepIndex = 0,
                                StepKind = AssertionLineageStepKind.ProductionMember,
                                Depth = 1,
                                SymbolDisplay = "Widget.Calculate()",
                                MemberId = candidateId,
                                Outcome = AssertionLineageStepOutcome.Traced,
                                ReasonCode = AssertionLineageReasonCodes.ProductionInvocation,
                                Summary = "Resolved production call."
                            }
                        ]
                    }
                ]
            }
        ]
    };
}
