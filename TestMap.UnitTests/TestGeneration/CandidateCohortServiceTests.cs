using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.TestGeneration;

public sealed class CandidateCohortServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void PrepareConfiguration_CreateRandomizedCohort_AssignsSeedAndSeries()
    {
        var config = CreateConfig(CandidateCohortMode.Create);

        CandidateCohortService.PrepareConfiguration(config);

        Assert.True(config.CandidateCohort.RandomSeed > 0);
        Assert.Equal("cohort-a", config.ExperimentSeriesId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildSelectionConfigurationHash_IgnoresEvaluationProviderChanges()
    {
        var first = CreateConfig(CandidateCohortMode.Create);
        first.IncludeProviders = ["CustomOpenAi"];
        var second = CreateConfig(CandidateCohortMode.Reuse);
        second.IncludeProviders = ["Anthropic"];

        var firstHash = CandidateCohortService.BuildSelectionConfigurationHash(first, new TestMapConfig());
        var secondHash = CandidateCohortService.BuildSelectionConfigurationHash(second, new TestMapConfig());

        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAndReuseAsync_PreservesSnapshotAndResolvesReingestedMember()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        await SeedSourceMemberAsync(db, memberId: 10);
        var service = new CandidateCohortService(db, new CandidateCohortRepository(db));
        var createConfig = CreateConfig(CandidateCohortMode.Create);
        createConfig.CandidateCohort.RandomSeed = 1234;
        var candidate = new CandidateMethod
        {
            MemberId = 10,
            ExistingTestMemberId = 20,
            CandidateInventoryId = 77,
            MethodName = "Run",
            SourceCode = "public void Run() {}",
            Signature = "public void Run()",
            BaselineCoverage = 0.42,
            ComplexityScore = 7,
            TestIntentionsSummary = "preserved intention",
            SelectionTime = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc)
        };

        var created = await service.CreateAsync(
            createConfig,
            new TestMapConfig(),
            projectId: 1,
            repositoryIdentity: "owner/repo",
            commitHash: "abc123",
            [candidate]);

        Assert.Single(created.Candidates);
        Assert.True(created.Candidates[0].CandidateCohortMemberId > 0);
        Assert.Equal(1, await db.CandidateCohorts.CountAsync());
        Assert.Equal(1, await db.CandidateCohortMembers.CountAsync());

        var oldMember = await db.Members.SingleAsync(x => x.Id == 10);
        db.Members.Remove(oldMember);
        await db.SaveChangesAsync();
        await SeedMemberOnlyAsync(db, memberId: 99);

        var reused = await service.ReuseAsync(
            CreateConfig(CandidateCohortMode.Reuse),
            new TestMapConfig(),
            projectId: 1,
            repositoryIdentity: "owner/repo",
            commitHash: "abc123");

        var reusedCandidate = Assert.Single(reused.Candidates);
        Assert.Equal(99, reusedCandidate.MemberId);
        Assert.Equal(0.42, reusedCandidate.BaselineCoverage);
        Assert.Equal(7, reusedCandidate.ComplexityScore);
        Assert.Equal("preserved intention", reusedCandidate.TestIntentionsSummary);
        Assert.Null(reusedCandidate.CandidateInventoryId);
        Assert.Equal(created.Cohort.Members[0].Id, reusedCandidate.CandidateCohortMemberId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_ExistingCohort_RequiresReuseMode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        await SeedSourceMemberAsync(db, memberId: 10);
        var service = new CandidateCohortService(db, new CandidateCohortRepository(db));
        var config = CreateConfig(CandidateCohortMode.Create);
        config.CandidateCohort.RandomSeed = 1234;
        var candidate = Candidate(memberId: 10);
        await service.CreateAsync(config, new TestMapConfig(), 1, "owner/repo", "abc123", [candidate]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(config, new TestMapConfig(), 1, "owner/repo", "abc123", [candidate]));

        Assert.Contains("mode 'reuse'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReuseAsync_DifferentCommit_IsRejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        await SeedSourceMemberAsync(db, memberId: 10);
        var service = new CandidateCohortService(db, new CandidateCohortRepository(db));
        var createConfig = CreateConfig(CandidateCohortMode.Create);
        createConfig.CandidateCohort.RandomSeed = 1234;
        await service.CreateAsync(
            createConfig,
            new TestMapConfig(),
            1,
            "owner/repo",
            "abc123",
            [Candidate(memberId: 10)]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReuseAsync(
                CreateConfig(CandidateCohortMode.Reuse),
                new TestMapConfig(),
                1,
                "owner/repo",
                "different"));

        Assert.Contains("abc123", exception.Message, StringComparison.Ordinal);
        Assert.Contains("different", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_UnmappedCandidate_IsRejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        await SeedSourceMemberAsync(db, memberId: 10);
        var service = new CandidateCohortService(db, new CandidateCohortRepository(db));
        var candidate = Candidate(memberId: 10);
        candidate.ExistingTestMemberId = null;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                CreateConfig(CandidateCohortMode.Create),
                new TestMapConfig(),
                1,
                "owner/repo",
                "abc123",
                [candidate]));

        Assert.Contains("no grounded source-to-test mapping", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.CandidateCohorts.ToListAsync());
    }

    private static ExperimentConfig CreateConfig(CandidateCohortMode mode)
    {
        return new ExperimentConfig
        {
            CandidateLimit = 1,
            CandidateCohort = new CandidateCohortConfig
            {
                Id = "cohort-a",
                Mode = mode
            }
        };
    }

    private static CandidateMethod Candidate(int memberId)
    {
        return new CandidateMethod
        {
            MemberId = memberId,
            ExistingTestMemberId = 20,
            MethodName = "Run",
            SourceCode = "public void Run() {}",
            Signature = "public void Run()",
            BaselineCoverage = 0.42,
            ComplexityScore = 7,
            SelectionTime = DateTime.UtcNow
        };
    }

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task SeedSourceMemberAsync(TestMapDbContext db, int memberId)
    {
        db.Projects.Add(new ProjectEntity
        {
            Id = 1,
            Owner = "owner",
            RepoName = "repo",
            DirectoryPath = ".",
            ContentHash = "project"
        });
        db.CSharpSolutions.Add(new CSharpSolutionEntity
        {
            Id = 1,
            ProjectId = 1,
            FilePath = "repo.sln",
            ContentHash = "solution"
        });
        db.CSharpProjects.Add(new CSharpProjectEntity
        {
            Id = 1,
            SolutionId = 1,
            FilePath = "src/App/App.csproj",
            ContentHash = "csharp-project"
        });
        db.Files.Add(new FileEntity
        {
            Id = 1,
            CSharpProjectId = 1,
            FilePath = "src/App/Runner.cs",
            ContentHash = "file"
        });
        db.Objects.Add(new ObjectEntity
        {
            Id = 1,
            FileId = 1,
            Namespace = "App",
            Name = "Runner",
            Kind = "class",
            FullString = "public class Runner {}",
            ContentHash = "object"
        });
        await SeedMemberOnlyAsync(db, memberId);
    }

    private static async Task SeedMemberOnlyAsync(TestMapDbContext db, int memberId)
    {
        db.Members.Add(new MemberEntity
        {
            Id = memberId,
            ObjectEntityId = 1,
            Name = "Run",
            Kind = "method",
            FullString = "public void Run() {}",
            ContentHash = "run-content"
        });
        await db.SaveChangesAsync();
    }
}
