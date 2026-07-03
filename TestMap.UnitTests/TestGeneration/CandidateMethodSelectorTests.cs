using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.TargetSelection.Strategies;
using Location = TestMap.Models.Code.Location;

namespace TestMap.UnitTests.TestGeneration;

/// <summary>
/// Verifies the hard-filter constraints in <see cref="CandidateMethodSelector"/>:
/// test members, generated members, test objects, test projects, and test-path files
/// are never surfaced as candidate targets regardless of coverage data.
/// </summary>
public sealed class CandidateMethodSelectorTests
{
    /// <summary>
    /// A member flagged IsTestMember=true is excluded from the candidate pool
    /// even when it has coverage data within the configured thresholds.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_TestMember_ExcludedFromCandidatePool()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        await SeedMemberAsync(db, memberId: 1, isTestMember: true, isTestObject: false,
            isTestProject: false, sourceFilePath: "src/Source/Svc.cs",
            sourceProjectPath: "src/Source/Source.csproj");
        await SeedCoverageAsync(db, memberId: 1, lineRate: 0.5);

        var selector = CreateSelector(db);
        var result = await selector.SelectAsync(DefaultExperimentConfig());

        Assert.Empty(result);
    }

    /// <summary>
    /// A member flagged IsGenerated=true is excluded from the candidate pool
    /// even when it has coverage data.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_GeneratedMember_ExcludedFromCandidatePool()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        await SeedMemberAsync(db, memberId: 1, isTestMember: false, isTestObject: false,
            isTestProject: false, sourceFilePath: "src/Source/Svc.cs",
            sourceProjectPath: "src/Source/Source.csproj", isGenerated: true);
        await SeedCoverageAsync(db, memberId: 1, lineRate: 0.5);

        var selector = CreateSelector(db);
        var result = await selector.SelectAsync(DefaultExperimentConfig());

        Assert.Empty(result);
    }

    /// <summary>
    /// A method in a class flagged IsTestObject=true is excluded from the candidate pool
    /// regardless of IsTestMember. Test helpers in test classes must not become targets.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_TestObject_ExcludedFromCandidatePool()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        await SeedMemberAsync(db, memberId: 1, isTestMember: false, isTestObject: true,
            isTestProject: false, sourceFilePath: "tests/Source.Tests/Helpers.cs",
            sourceProjectPath: "tests/Source.Tests/Source.Tests.csproj");
        await SeedCoverageAsync(db, memberId: 1, lineRate: 0.5);

        var selector = CreateSelector(db);
        var result = await selector.SelectAsync(DefaultExperimentConfig());

        Assert.Empty(result);
    }

    /// <summary>
    /// A method in a project flagged BuildMetadata.IsTestProject=true is excluded
    /// from the candidate pool even if the member itself is not flagged as a test member.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_TestProject_ExcludedFromCandidatePool()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        await SeedMemberAsync(db, memberId: 1, isTestMember: false, isTestObject: false,
            isTestProject: true, sourceFilePath: "tests/Source.Tests/Helpers.cs",
            sourceProjectPath: "tests/Source.Tests/Source.Tests.csproj");
        await SeedCoverageAsync(db, memberId: 1, lineRate: 0.5);

        var selector = CreateSelector(db);
        var result = await selector.SelectAsync(DefaultExperimentConfig());

        Assert.Empty(result);
    }

    /// <summary>
    /// A method whose file path contains "/tests/" is excluded from the candidate pool
    /// by the IsLikelyTestPath heuristic.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_TestFilePath_ExcludedFromCandidatePool()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        // Explicitly not a test project by metadata, but under a /tests/ directory
        await SeedMemberAsync(db, memberId: 1, isTestMember: false, isTestObject: false,
            isTestProject: false, sourceFilePath: "tests/Helpers/Util.cs",
            sourceProjectPath: "tests/Helpers/Helpers.csproj");
        await SeedCoverageAsync(db, memberId: 1, lineRate: 0.5);

        var selector = CreateSelector(db);
        var result = await selector.SelectAsync(DefaultExperimentConfig());

        Assert.Empty(result);
    }

    /// <summary>
    /// A member with coverage below the configured MinCoverageThreshold is excluded
    /// from the candidate pool; only members within [min, max] are included.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_CoverageBelowMinThreshold_ExcludedFromCandidatePool()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        await SeedMemberAsync(db, memberId: 1, isTestMember: false, isTestObject: false,
            isTestProject: false, sourceFilePath: "src/Source/Svc.cs",
            sourceProjectPath: "src/Source/Source.csproj");
        await SeedCoverageAsync(db, memberId: 1, lineRate: 0.1);

        var config = DefaultExperimentConfig();
        config.MinCoverageThreshold = 0.5;
        config.MaxCoverageThreshold = 1.0;
        var selector = CreateSelector(db);
        var result = await selector.SelectAsync(config);

        Assert.Empty(result);
    }

    /// <summary>
    /// A production method with coverage within the configured thresholds is included
    /// in the candidate pool and returned by the strategy.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_ProductionMemberWithCoverageInRange_IncludedInCandidatePool()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        await SeedMemberAsync(db, memberId: 1, isTestMember: false, isTestObject: false,
            isTestProject: false, sourceFilePath: "src/Source/Svc.cs",
            sourceProjectPath: "src/Source/Source.csproj");
        await SeedCoverageAsync(db, memberId: 1, lineRate: 0.6);

        var selector = CreateSelector(db);
        var result = await selector.SelectAsync(DefaultExperimentConfig());

        var candidate = Assert.Single(result);
        Assert.Equal(1, candidate.MemberId);
    }

    /// <summary>
    /// When both a production member and a test member have coverage, only the
    /// production member is included; the test member is hard-filtered regardless.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_MixedPool_OnlyProductionMemberIncluded()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        // Production member
        await SeedMemberAsync(db, memberId: 1, isTestMember: false, isTestObject: false,
            isTestProject: false, sourceFilePath: "src/Source/Svc.cs",
            sourceProjectPath: "src/Source/Source.csproj");
        await SeedCoverageAsync(db, memberId: 1, lineRate: 0.4, coverageId: 1);

        // Test member with coverage — must be excluded
        await SeedMemberAsync(db, memberId: 2, isTestMember: true, isTestObject: true,
            isTestProject: true, sourceFilePath: "tests/Source.Tests/SvcTests.cs",
            sourceProjectPath: "tests/Source.Tests/Source.Tests.csproj",
            objectId: 2, memberId2: 2);
        await SeedCoverageAsync(db, memberId: 2, lineRate: 0.4, coverageId: 2);

        var selector = CreateSelector(db);
        var result = await selector.SelectAsync(DefaultExperimentConfig());

        var candidate = Assert.Single(result);
        Assert.Equal(1, candidate.MemberId);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task SeedMemberAsync(
        TestMapDbContext db,
        int memberId,
        bool isTestMember,
        bool isTestObject,
        bool isTestProject,
        string sourceFilePath,
        string sourceProjectPath,
        bool isGenerated = false,
        int objectId = 1,
        int memberId2 = 1)
    {
        if (!db.CSharpSolutions.Any())
        {
            db.CSharpSolutions.Add(new CSharpSolutionEntity
            {
                Id = 1, ProjectId = 1, FilePath = "repo.sln", ContentHash = "solution"
            });
            db.Projects.Add(new ProjectEntity
            {
                Id = 1, Owner = "o", RepoName = "r", DirectoryPath = ".", ContentHash = "proj"
            });
        }

        var projectId = memberId + 100;
        db.CSharpProjects.Add(new CSharpProjectEntity
        {
            Id = projectId,
            SolutionId = 1,
            FilePath = sourceProjectPath,
            BuildMetadata = new ProjectBuildMetadataModel
            {
                IsTestProject = isTestProject,
                DefaultBuildTarget = "net10.0"
            },
            ContentHash = $"project-{projectId}"
        });

        var fileId = memberId + 200;
        db.Files.Add(new FileEntity
        {
            Id = fileId, CSharpProjectId = projectId, FilePath = sourceFilePath,
            ContentHash = $"file-{fileId}"
        });

        db.Objects.Add(new ObjectEntity
        {
            Id = objectId, FileId = fileId,
            Namespace = "Ns", Name = "Cls", Kind = "class",
            IsTestObject = isTestObject, FullString = "public class Cls {}",
            ContentHash = $"object-{objectId}"
        });

        db.Members.Add(new MemberEntity
        {
            Id = memberId, ObjectEntityId = objectId,
            Name = "Method", Kind = "method",
            Modifiers = isTestMember ? ["public"] : ["public"],
            IsTestMember = isTestMember,
            IsGenerated = isGenerated,
            FullString = "public void Method() {}",
            Location = new Location(1, 0, 1, 0),
            ContentHash = $"member-{memberId}"
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedCoverageAsync(
        TestMapDbContext db,
        int memberId,
        double lineRate,
        int coverageId = 1)
    {
        if (!db.CoverageReports.Any(x => x.Id == coverageId))
        {
            db.CoverageReports.Add(new CoverageReportEntity
            {
                Id = coverageId, ProjectId = 1, LineRate = lineRate,
                BranchRate = 0.0, Complexity = 1, Version = "test",
                Timestamp = 1, LinesValid = 10
            });
        }

        db.MemberCoverages.Add(new MemberCoverageEntity
        {
            Id = coverageId + 1000,
            MemberId = memberId,
            CoverageReportId = coverageId,
            LineRate = lineRate,
            LinesValid = 10,
            Complexity = 1
        });

        await db.SaveChangesAsync();
    }

    private static CandidateMethodSelector CreateSelector(TestMapDbContext db)
    {
        var config = new TestMapConfig();
        var project = new ProjectModel(config: config) { DbId = 1 };
        var context = new ProjectContext(project);
        return new CandidateMethodSelector(
            context,
            db,
            config,
            [new ExistingCandidateSelectionStrategy()]);
    }

    private static ExperimentConfig DefaultExperimentConfig() => new ExperimentConfig
    {
        CandidateLimit = 10,
        MinCoverageThreshold = 0.0,
        MaxCoverageThreshold = 1.0,
        CandidateSelectionStrategy = TargetSelectionStrategy.Existing
    };
}
