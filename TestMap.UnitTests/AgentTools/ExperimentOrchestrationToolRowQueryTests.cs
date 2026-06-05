using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.AgentTools;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Entities.MutationTesting;
using TestMap.Persistence.Ef.Entities.Testing;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for the internal query helpers on <see cref="ExperimentOrchestrationService"/>
/// that feed coverage/mutation data and per-test rows into the tool-lane CSV output.
/// </summary>
public sealed class ExperimentOrchestrationToolRowQueryTests
{
    // ─── GetTestRunMetricsAsync ───────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetTestRunMetrics_NullTestRunId_ReturnsBothNull()
    {
        await using var db = await CreateDbAsync();
        var svc = MakeService(db);

        var (cov, mut) = await svc.GetTestRunMetricsAsync(null, default);

        Assert.Null(cov);
        Assert.Null(mut);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetTestRunMetrics_NoReports_ReturnsBothNull()
    {
        await using var db = await CreateDbAsync();
        var svc = MakeService(db);

        var (cov, mut) = await svc.GetTestRunMetricsAsync(999, default);

        Assert.Null(cov);
        Assert.Null(mut);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetTestRunMetrics_WithCoverageAndMutation_ReturnsBothValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        var testRun = new TestRunEntity { ProjectId = 1, RunId = "run1", RunDate = "2026-06-04", Success = true };
        db.TestRuns.Add(testRun);
        await db.SaveChangesAsync();

        db.CoverageReports.Add(new CoverageReportEntity
        {
            ProjectId = 1,
            TestRunId = testRun.Id,
            LineRate = 0.82,
            BranchRate = 0.7,
            Complexity = 1.0,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        db.MutationTestingReports.Add(new MutationTestingReportEntity
        {
            ProjectId = 1,
            TestRunId = testRun.Id,
            MutationScore = 0.55,
            SourceProjectPath = "/src",
            TestProjectPath = "/tests"
        });
        await db.SaveChangesAsync();

        var svc = MakeService(db);

        var (cov, mut) = await svc.GetTestRunMetricsAsync(testRun.Id, default);

        Assert.Equal(0.82, cov);
        Assert.Equal(0.55, mut);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetTestRunMetrics_CoverageOnly_ReturnsCoverageNullMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        var testRun = new TestRunEntity { ProjectId = 1, RunId = "run2", RunDate = "2026-06-04", Success = true };
        db.TestRuns.Add(testRun);
        await db.SaveChangesAsync();

        db.CoverageReports.Add(new CoverageReportEntity
        {
            ProjectId = 1,
            TestRunId = testRun.Id,
            LineRate = 0.60,
            BranchRate = 0.5,
            Complexity = 1.0,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        await db.SaveChangesAsync();

        var svc = MakeService(db);

        var (cov, mut) = await svc.GetTestRunMetricsAsync(testRun.Id, default);

        Assert.Equal(0.60, cov);
        Assert.Null(mut);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMemberCoverageForTestRun_WithMemberCoverage_ReturnsMethodLineRate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        var testRun = new TestRunEntity { ProjectId = 1, RunId = "run-member", RunDate = "2026-06-04", Success = true };
        db.TestRuns.Add(testRun);
        await db.SaveChangesAsync();

        var report = new CoverageReportEntity
        {
            ProjectId = 1,
            TestRunId = testRun.Id,
            LineRate = 0.90,
            BranchRate = 0.7,
            Complexity = 1.0,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        db.CoverageReports.Add(report);
        await db.SaveChangesAsync();

        db.MemberCoverages.AddRange(
            new MemberCoverageEntity
            {
                CoverageReportId = report.Id,
                MemberId = 42,
                LineRate = 0.65,
                BranchRate = 0.5,
                LinesCovered = 13,
                LinesValid = 20
            },
            new MemberCoverageEntity
            {
                CoverageReportId = report.Id,
                MemberId = 43,
                LineRate = 1.0,
                BranchRate = 1.0,
                LinesCovered = 4,
                LinesValid = 4
            });
        await db.SaveChangesAsync();

        var svc = MakeService(db);

        var coverage = await svc.GetMemberCoverageForTestRunAsync(testRun.Id, 42, default);

        Assert.Equal(0.65, coverage);
    }

    // ─── GetPostAttemptTestResultsAsync ──────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPostAttemptTestResults_NoResults_ReturnsEmpty()
    {
        await using var db = await CreateDbAsync();
        var svc = MakeService(db);

        var results = await svc.GetPostAttemptTestResultsAsync(42, default);

        Assert.Empty(results);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPostAttemptTestResults_MultipleResults_ReturnsSortedByName()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        var testRun = new TestRunEntity { ProjectId = 1, RunId = "run3", RunDate = "2026-06-04", Success = true };
        db.TestRuns.Add(testRun);
        await db.SaveChangesAsync();

        db.TestResults.AddRange(
            new TestResultEntity { TestRunId = testRun.Id, RunId = "run3", RunDate = "2026-06-04", TestName = "Zeta_Test", Outcome = "Failed" },
            new TestResultEntity { TestRunId = testRun.Id, RunId = "run3", RunDate = "2026-06-04", TestName = "Alpha_Test", Outcome = "Passed" },
            new TestResultEntity { TestRunId = testRun.Id, RunId = "run3", RunDate = "2026-06-04", TestName = "Mu_Test", Outcome = "Passed" });
        await db.SaveChangesAsync();

        var svc = MakeService(db);

        var results = await svc.GetPostAttemptTestResultsAsync(testRun.Id, default);

        Assert.Equal(3, results.Count);
        Assert.Equal("Alpha_Test", results[0].TestName);
        Assert.Equal("Passed", results[0].Outcome);
        Assert.Equal("Mu_Test", results[1].TestName);
        Assert.Equal("Zeta_Test", results[2].TestName);
        Assert.Equal("Failed", results[2].Outcome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPostAttemptTestResults_WrongRunId_ReturnsEmpty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        var testRun = new TestRunEntity { ProjectId = 1, RunId = "run4", RunDate = "2026-06-04", Success = true };
        db.TestRuns.Add(testRun);
        await db.SaveChangesAsync();

        db.TestResults.Add(new TestResultEntity
        {
            TestRunId = testRun.Id,
            RunId = "run4",
            RunDate = "2026-06-04",
            TestName = "SomeTest",
            Outcome = "Passed"
        });
        await db.SaveChangesAsync();

        var svc = MakeService(db);

        // Query with a different run id.
        var results = await svc.GetPostAttemptTestResultsAsync(testRun.Id + 1000, default);

        Assert.Empty(results);
    }

    // ─── GetLinkedTestMembersAsync ────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLinkedTestMembers_NoLinks_ReturnsEmpty()
    {
        await using var db = await CreateDbAsync();
        var svc = MakeService(db);

        var members = await svc.GetLinkedTestMembersAsync(999, default);

        Assert.Empty(members);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLinkedTestMembers_WithLinks_ReturnsMemberIdAndNameSorted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        var (experimentRunId, candidateId) = await SeedExperimentGraphAsync(db);
        var toolAttemptId = await SeedToolAttemptAsync(db, experimentRunId, candidateId);

        var csproj = new CSharpProjectEntity { FilePath = "/tests/T.csproj", SolutionId = 0 };
        db.CSharpProjects.Add(csproj);
        await db.SaveChangesAsync();
        var file = new FileEntity { CSharpProjectId = csproj.Id, FilePath = "/tests/T.cs" };
        db.Files.Add(file);
        await db.SaveChangesAsync();
        var obj = new ObjectEntity { FileId = file.Id, Name = "Tests", Kind = "class", IsTestObject = true };
        db.Objects.Add(obj);
        await db.SaveChangesAsync();
        var memberZ = new MemberEntity { ObjectEntityId = obj.Id, Name = "ZetaTest", Kind = "method", IsTestMember = true };
        var memberA = new MemberEntity { ObjectEntityId = obj.Id, Name = "AlphaTest", Kind = "method", IsTestMember = true };
        db.Members.AddRange(memberZ, memberA);
        await db.SaveChangesAsync();
        db.ToolAttemptGeneratedTests.AddRange(
            new ToolAttemptGeneratedTestEntity { ToolAttemptId = toolAttemptId, MemberId = memberZ.Id },
            new ToolAttemptGeneratedTestEntity { ToolAttemptId = toolAttemptId, MemberId = memberA.Id });
        await db.SaveChangesAsync();

        var svc = MakeService(db);

        var members = await svc.GetLinkedTestMembersAsync(toolAttemptId, default);

        Assert.Equal(2, members.Count);
        Assert.Equal("AlphaTest", members[0].Name);
        Assert.Equal(memberA.Id, members[0].MemberId);
        Assert.Equal("ZetaTest", members[1].Name);
        Assert.Equal(memberZ.Id, members[1].MemberId);
    }

    // ─── StripTestArguments ───────────────────────────────────────────────────

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("TestMethod", "TestMethod")]
    [InlineData("Ns.Class.TestMethod", "Ns.Class.TestMethod")]
    [InlineData("TestMethod(x: 1)", "TestMethod")]
    [InlineData("Ns.Class.TestMethod(x: 1, y: \"hello\")", "Ns.Class.TestMethod")]
    [InlineData("TestMethod (x: 1)", "TestMethod")]
    public void StripTestArguments_VariousFormats_StripsAtFirstParen(string input, string expected)
    {
        var result = ExperimentOrchestrationService.StripTestArguments(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectGeneratedPostAttemptTestResults_FiltersToLinkedGeneratedTests()
    {
        var results = new List<(string TestName, string Outcome)>
        {
            ("TestMap_Example.Tests.StudentTest.TestStudentConstructor", "Passed"),
            ("ProgramTest.TestStartAddStudent", "Passed"),
            ("ProgramTest.TestStartInvalidChoice(value: 9)", "Failed")
        };
        var linkedMembers = new List<(int MemberId, string Name)>
        {
            (10, "TestStartAddStudent"),
            (11, "TestStartInvalidChoice")
        };

        var selected = ExperimentOrchestrationService.SelectGeneratedPostAttemptTestResults(
            results,
            linkedMembers);

        Assert.Equal(2, selected.Count);
        Assert.Equal("ProgramTest.TestStartAddStudent", selected[0].TestName);
        Assert.Equal("ProgramTest.TestStartInvalidChoice(value: 9)", selected[1].TestName);
    }

    // ─── GetLinkedTestMemberNamesAsync ────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLinkedTestMemberNames_NoLinks_ReturnsEmpty()
    {
        await using var db = await CreateDbAsync();
        var svc = MakeService(db);

        var names = await svc.GetLinkedTestMemberNamesAsync(999, default);

        Assert.Empty(names);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLinkedTestMemberNames_WithLinks_ReturnsMemberNamesSorted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        var (experimentRunId, candidateId) = await SeedExperimentGraphAsync(db);
        var toolAttemptId = await SeedToolAttemptAsync(db, experimentRunId, candidateId);

        // Seed two test members via the same code-graph pattern as ToolAttemptGeneratedTestServiceTests.
        var csproj = new CSharpProjectEntity { FilePath = "/tests/T.csproj", SolutionId = 0 };
        db.CSharpProjects.Add(csproj);
        await db.SaveChangesAsync();

        var file = new FileEntity { CSharpProjectId = csproj.Id, FilePath = "/tests/T.cs" };
        db.Files.Add(file);
        await db.SaveChangesAsync();

        var obj = new ObjectEntity { FileId = file.Id, Name = "Tests", Kind = "class", IsTestObject = true };
        db.Objects.Add(obj);
        await db.SaveChangesAsync();

        var memberZ = new MemberEntity { ObjectEntityId = obj.Id, Name = "ZetaTest", Kind = "method", IsTestMember = true };
        var memberA = new MemberEntity { ObjectEntityId = obj.Id, Name = "AlphaTest", Kind = "method", IsTestMember = true };
        db.Members.AddRange(memberZ, memberA);
        await db.SaveChangesAsync();

        db.ToolAttemptGeneratedTests.AddRange(
            new ToolAttemptGeneratedTestEntity { ToolAttemptId = toolAttemptId, MemberId = memberZ.Id },
            new ToolAttemptGeneratedTestEntity { ToolAttemptId = toolAttemptId, MemberId = memberA.Id });
        await db.SaveChangesAsync();

        var svc = MakeService(db);

        var names = await svc.GetLinkedTestMemberNamesAsync(toolAttemptId, default);

        Assert.Equal(2, names.Count);
        Assert.Equal("AlphaTest", names[0]); // sorted alphabetically
        Assert.Equal("ZetaTest", names[1]);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection? connection = null)
    {
        connection ??= new SqliteConnection("Data Source=:memory:");
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<(int ExperimentRunId, int CandidateMethodId)> SeedExperimentGraphAsync(
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
            SourceMemberId = 1,
            SourceMethodName = "Foo",
            SourceMethodSignature = "void Foo()"
        };
        db.CandidateMethods.Add(candidate);
        await db.SaveChangesAsync();

        return (run.Id, candidate.Id);
    }

    private static async Task<int> SeedToolAttemptAsync(
        TestMapDbContext db,
        int experimentRunId,
        int candidateMethodId)
    {
        var entity = new ToolAttemptEntity
        {
            ExperimentRunId = experimentRunId,
            CandidateMethodId = candidateMethodId,
            ToolId = "codex",
            RunStatus = "Completed",
            ValidationOutcome = "Passed",
            ObservedOutcome = "ValidatedEvidencePositive",
            ImageName = "img",
            ImageKey = "codex",
            StartedAt = DateTime.UtcNow,
            TimeoutSeconds = 2700
        };
        db.ToolAttempts.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    /// <summary>
    /// Creates a minimal <see cref="ExperimentOrchestrationService"/> with only
    /// <paramref name="db"/> wired up. All other dependencies are null because the
    /// three internal query helpers under test only use <c>_dbContext</c>.
    /// </summary>
    private static ExperimentOrchestrationService MakeService(TestMapDbContext db) =>
        new(
            context: new TestMap.App.ProjectContext(new ProjectModel()),
            config: new TestMapConfig(),
            methodSelection: null!,
            pipeline: null!,
            experimentRunRepo: null!,
            workItemRepo: null!,
            candidateMethodRepo: null!,
            attemptRepo: null!,
            stepRepo: null!,
            executionRepo: null!,
            riskScoreRepo: null!,
            generatedTestExecutionService: null!,
            generationValidationService: null!,
            generationClassificationService: null!,
            matrixGenerator: null!,
            budgetExecutor: null!,
            buildTestService: null!,
            resumeService: null!,
            ruleDecisionRecorder: null!,
            resultsWriter: null!,
            artifactCleanupService: null!,
            dbContext: db,
            dockerToolRunner: null!,
            agentToolEnvironmentResolver: null!,
            toolAttemptRepo: null!,
            taskCardWriter: null!,
            targetedBaselineService: null!,
            toolPostAttemptAnalysisService: null!,
            toolAttemptGeneratedTestService: null!,
            toolPostAttemptMeasurementService: null!,
            generationApproaches: [],
            workspace: null!);
}
