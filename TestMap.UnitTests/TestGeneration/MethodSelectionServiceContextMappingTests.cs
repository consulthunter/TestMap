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
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.TargetSelection.Strategies;

namespace TestMap.UnitTests.TestGeneration;

public sealed class MethodSelectionServiceContextMappingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_DirectInvocationOnly_MapsDirectlyInvokingTest()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: true);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.Equal(20, context.Method.ExistingTestMemberId);
        Assert.Equal("Target_InvokesProductionMethod", context.Method.ExistingTestMethodName);
        Assert.Contains("Context mapping: DirectInvocation", context.Method.TestStateReason);
        Assert.Contains("directly invokes source member", context.Method.TestStateReason);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_DirectInvocationOnly_IgnoresHeuristicContextWithoutInvocation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.Null(context.Method.ExistingTestMemberId);
        Assert.Null(context.Method.ExistingTestMethodName);
        Assert.Contains("Context mapping: no test context selected.", context.Method.TestStateReason);
        Assert.Equal("TargetServiceTests", context.TestClassName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectCandidateMethodsAsync_PersistsAllCandidatesAndSelectsGroundedContextAfterEnrichment()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedSelectionProjectAsync(db);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var selected = await service.SelectCandidateMethodsAsync(new ExperimentConfig
        {
            CandidateLimit = 1,
            MaxCoverageThreshold = 1.0,
            ContextMappingMode = TestContextMappingMode.DirectInvocationOnly
        });

        var candidate = Assert.Single(selected);
        Assert.Equal(11, candidate.MemberId);
        Assert.Equal(21, candidate.ExistingTestMemberId);
        Assert.Equal(2, await db.CandidateInventory.CountAsync());
    }

    private static async Task<TestMapDbContext> CreateDatabaseAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static MethodSelectionService CreateService(
        TestMapDbContext db,
        TestContextMappingMode mappingMode)
    {
        var config = new TestMapConfig();
        config.TestingConfig.GenerationConfig.TargetSelection.ContextMappingMode = mappingMode;
        var project = new ProjectModel(config: config)
        {
            DbId = 1,
            Projects =
            [
                new CSharpProjectModel([], [], [])
                {
                    Id = 1,
                    SolutionId = 1,
                    FilePath = "src/Source/Source.csproj",
                    BuildMetadata = new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" }
                },
                new CSharpProjectModel([], [], [])
                {
                    Id = 2,
                    SolutionId = 1,
                    FilePath = "tests/Source.Tests/Source.Tests.csproj",
                    BuildMetadata = new ProjectBuildMetadataModel
                    {
                        IsTestProject = true,
                        DefaultBuildTarget = "net10.0",
                        Notes = "xunit"
                    }
                }
            ]
        };
        var projectContext = new ProjectContext(project);
        var selector = new CandidateMethodSelector(
            projectContext,
            db,
            config,
            [new ExistingCandidateSelectionStrategy()]);

        return new MethodSelectionService(
            projectContext,
            db,
            selector,
            new CandidateInventoryRepository(db));
    }

    private static async Task SeedProjectAsync(TestMapDbContext db, bool includeDirectInvocation)
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
        db.CSharpProjects.AddRange(
            new CSharpProjectEntity
            {
                Id = 1,
                SolutionId = 1,
                FilePath = "src/Source/Source.csproj",
                BuildMetadata = new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" },
                ContentHash = "source-project"
            },
            new CSharpProjectEntity
            {
                Id = 2,
                SolutionId = 1,
                FilePath = "tests/Source.Tests/Source.Tests.csproj",
                BuildMetadata = new ProjectBuildMetadataModel
                {
                    IsTestProject = true,
                    DefaultBuildTarget = "net10.0",
                    Notes = "xunit"
                },
                ContentHash = "test-project"
            });
        db.Files.AddRange(
            new FileEntity
            {
                Id = 1,
                CSharpProjectId = 1,
                FilePath = "src/Source/TargetService.cs",
                ContentHash = "source-file"
            },
            new FileEntity
            {
                Id = 2,
                CSharpProjectId = 2,
                FilePath = "tests/Source.Tests/TargetServiceTests.cs",
                UsingStatements = ["using Xunit;"],
                ContentHash = "test-file"
            });
        db.Objects.AddRange(
            new ObjectEntity
            {
                Id = 1,
                FileId = 1,
                Namespace = "Source",
                Name = "TargetService",
                Kind = "class",
                FullString = "namespace Source; public class TargetService { public int Target() => 1; }",
                ContentHash = "source-object"
            },
            new ObjectEntity
            {
                Id = 2,
                FileId = 2,
                Namespace = "Source.Tests",
                Name = "TargetServiceTests",
                Kind = "class",
                IsTestObject = true,
                TestFramework = "xUnit",
                FullString = "namespace Source.Tests; public class TargetServiceTests { [Fact] public void Target_InvokesProductionMethod() { new Source.TargetService().Target(); } }",
                ContentHash = "test-object"
            });
        db.Members.AddRange(
            new MemberEntity
            {
                Id = 10,
                ObjectEntityId = 1,
                Name = "Target",
                Kind = "method",
                FullString = "public int Target() => 1;",
                Location = new Location(1, 1, 1, 1),
                ContentHash = "source-member"
            },
            new MemberEntity
            {
                Id = 20,
                ObjectEntityId = 2,
                Name = "Target_InvokesProductionMethod",
                Kind = "method",
                IsTestMember = true,
                FullString = "[Fact] public void Target_InvokesProductionMethod() { new Source.TargetService().Target(); }",
                Location = new Location(1, 1, 1, 1),
                ContentHash = "test-member"
            });

        if (includeDirectInvocation)
        {
            db.Invocations.Add(new InvocationEntity
            {
                Id = 1,
                MemberId = 20,
                InvokedMemberId = 10,
                FullString = "Target()",
                ContentHash = "invocation"
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedSelectionProjectAsync(TestMapDbContext db)
    {
        await SeedProjectAsync(db, includeDirectInvocation: false);

        db.Objects.Add(new ObjectEntity
        {
            Id = 3,
            FileId = 1,
            Namespace = "Source",
            Name = "DirectService",
            Kind = "class",
            FullString = "namespace Source; public class DirectService { public int DirectTarget() => 1; }",
            ContentHash = "direct-source-object"
        });
        db.Members.Add(new MemberEntity
        {
            Id = 11,
            ObjectEntityId = 3,
            Name = "DirectTarget",
            Kind = "method",
            FullString = "public int DirectTarget() => 1;",
            Location = new Location(2, 2, 2, 2),
            ContentHash = "direct-source-member"
        });
        db.Members.Add(new MemberEntity
        {
            Id = 21,
            ObjectEntityId = 2,
            Name = "DirectTarget_InvokesProductionMethod",
            Kind = "method",
            IsTestMember = true,
            FullString = "[Fact] public void DirectTarget_InvokesProductionMethod() { new Source.DirectService().DirectTarget(); }",
            Location = new Location(2, 2, 2, 2),
            ContentHash = "direct-test-member"
        });
        db.Invocations.Add(new InvocationEntity
        {
            Id = 2,
            MemberId = 21,
            InvokedMemberId = 11,
            FullString = "DirectTarget()",
            ContentHash = "direct-invocation"
        });
        db.CoverageReports.Add(new CoverageReportEntity
        {
            Id = 1,
            ProjectId = 1,
            LineRate = 0.5,
            BranchRate = 0.0,
            Complexity = 1,
            Version = "test",
            Timestamp = 1,
            LinesCovered = 1,
            LinesValid = 2
        });
        db.MemberCoverages.AddRange(
            new MemberCoverageEntity
            {
                Id = 1,
                MemberId = 10,
                CoverageReportId = 1,
                LineRate = 0.0,
                LinesValid = 1,
                Complexity = 1
            },
            new MemberCoverageEntity
            {
                Id = 2,
                MemberId = 11,
                CoverageReportId = 1,
                LineRate = 0.9,
                LinesCovered = 9,
                LinesValid = 10,
                Complexity = 1
            });

        await db.SaveChangesAsync();
    }
}
