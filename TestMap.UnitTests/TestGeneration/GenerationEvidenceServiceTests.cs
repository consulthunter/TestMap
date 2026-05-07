using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Entities.MutationTesting;
using TestMap.Rules.Generation;
using TestMap.Services.TestGeneration.Evidence;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GenerationEvidenceServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_Naive_SuppressesExplicitMetricEvidence()
    {
        await using var fixture = await EvidenceDbFixture.CreateAsync();
        var service = new GenerationEvidenceService(fixture.DbContext);

        var package = await service.BuildAsync(new GenerationEvidenceOptions
        {
            CandidateContext = CreateCandidateContext(),
            Approach = TestGenerationApproach.Naive,
            MetricsPath = MetricsDrivenPath.CoverageAndMutation
        });

        Assert.Null(package.Coverage);
        Assert.Null(package.Mutation);
        Assert.Contains(package.RuleDecisions, x =>
            x.RuleId == GenerationEvidenceRuleDefinitions.ProjectContextIncluded.Id);
        Assert.Contains(package.RuleDecisions, x =>
            x.RuleId == GenerationEvidenceRuleDefinitions.NaiveSuppressesMetricEvidence.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_MetricsDrivenCoverageAndMutation_IncludesAvailableEvidence()
    {
        await using var fixture = await EvidenceDbFixture.CreateAsync();
        fixture.DbContext.MemberCoverages.Add(new MemberCoverageEntity
        {
            MemberId = 7,
            CoverageReportId = 99,
            LineRate = 0.5,
            BranchRate = 0.25,
            LinesCovered = 5,
            LinesValid = 10,
            BranchesCovered = 1,
            BranchesValid = 4
        });
        fixture.DbContext.CoverageGaps.Add(new CoverageGapEntity
        {
            MemberId = 7,
            CoverageReportId = 99,
            LineNumber = 12,
            GapKind = "UncoveredLine",
            SourceText = "return false;"
        });
        fixture.DbContext.Mutants.Add(new MutantEntity
        {
            MemberId = 7,
            StrykerMutantId = "1",
            MutatorName = "EqualityOperator",
            Replacement = ">=",
            Status = "Survived",
            Location = new Location(14, 0, 14, 10),
            CoveredBy = ["CalculatorTests.Add_ReturnsSum"]
        });
        await fixture.DbContext.SaveChangesAsync();
        var service = new GenerationEvidenceService(fixture.DbContext);

        var package = await service.BuildAsync(new GenerationEvidenceOptions
        {
            CandidateContext = CreateCandidateContext(),
            Approach = TestGenerationApproach.MetricsDriven,
            MetricsPath = MetricsDrivenPath.CoverageAndMutation
        });

        Assert.NotNull(package.Coverage);
        Assert.Equal(0.5, package.Coverage.CurrentLineCoverage);
        var gap = Assert.Single(package.Coverage.Gaps);
        Assert.Equal(12, gap.LineNumber);
        Assert.NotNull(package.Mutation);
        var mutant = Assert.Single(package.Mutation.SurvivingMutants);
        Assert.Equal("1", mutant.MutantId);
        Assert.Equal(14, mutant.StartLine);
        Assert.Contains(package.RuleDecisions, x =>
            x.RuleId == GenerationEvidenceRuleDefinitions.CoverageEvidenceIncluded.Id);
        Assert.Contains(package.RuleDecisions, x =>
            x.RuleId == GenerationEvidenceRuleDefinitions.MutationEvidenceIncluded.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_MetricsDrivenCoverage_RecordsUnavailableCoverageEvidence()
    {
        await using var fixture = await EvidenceDbFixture.CreateAsync();
        var service = new GenerationEvidenceService(fixture.DbContext);

        var package = await service.BuildAsync(new GenerationEvidenceOptions
        {
            CandidateContext = CreateCandidateContext(),
            Approach = TestGenerationApproach.MetricsDriven,
            MetricsPath = MetricsDrivenPath.Coverage
        });

        Assert.NotNull(package.Coverage);
        Assert.Empty(package.Coverage.Gaps);
        Assert.Null(package.Mutation);
        Assert.Contains(package.RuleDecisions, x =>
            x.RuleId == GenerationEvidenceRuleDefinitions.CoverageEvidenceUnavailable.Id);
    }

    private static CandidateMethodContext CreateCandidateContext()
    {
        return new CandidateMethodContext
        {
            Method = new CandidateMethod
            {
                MemberId = 7,
                MethodName = "Add",
                SourceCode = "public int Add(int x, int y) => x + y;",
                Signature = "public int Add(int x, int y)",
                BaselineCoverage = 0.42
            },
            MethodSignature = "public int Add(int x, int y)",
            ContainingClass = "public class Calculator { }",
            TestNamespace = "Sample.Tests",
            TestClassName = "CalculatorTests",
            TestFilePath = "CalculatorTests.cs",
            SourceFilePath = "Calculator.cs",
            SourceLocation = new CandidateSourceLocation
            {
                SourceFilePath = "Calculator.cs",
                StartLine = 1,
                EndLine = 1
            },
            SourceProjectPath = "Sample.csproj",
            TestProjectPath = "Sample.Tests.csproj",
            TargetBuildFramework = "net10.0",
            SolutionFilePath = "Sample.sln",
            ExampleTest = "[Fact] public void Existing() { }",
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = "public class CalculatorTests { }",
            TestFileContents = "public class CalculatorTests { }",
            TestSupportContext = string.Empty,
            TestFramework = "xUnit",
            TestDependencies = "using Xunit;",
            CoverageGapSummary = string.Empty
        };
    }

    private sealed class EvidenceDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private EvidenceDbFixture(SqliteConnection connection, TestMapDbContext dbContext)
        {
            _connection = connection;
            DbContext = dbContext;
        }

        public TestMapDbContext DbContext { get; }

        public static async Task<EvidenceDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<TestMapDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new TestMapDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            return new EvidenceDbFixture(connection, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
