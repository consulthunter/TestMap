using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Services.RiskScoring;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.TargetSelection.Strategies;
using Location = TestMap.Models.Code.Location;

namespace TestMap.UnitTests.TestGeneration;

/// <summary>
/// Verifies that <see cref="RiskWeightedCandidateSelectionStrategy"/> ranks candidates by
/// risk score descending, breaking ties by line rate ascending, and passes the full candidate
/// pool to the output (it ranks — it does not filter by threshold).
/// </summary>
public sealed class RiskWeightedCandidateSelectionStrategyTests
{
    /// <summary>
    /// Candidates are ordered by risk score descending so that the highest-risk method
    /// is returned first, regardless of its insertion order in the pool.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_OrdersByRiskScoreDescending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedMembersAsync(db, [1, 2, 3]);

        // Scores: member 2 has highest risk, member 3 lowest
        var riskService = new FixedRiskScoringService(new Dictionary<int, double>
        {
            [1] = 0.5,
            [2] = 0.9,
            [3] = 0.2
        });
        var strategy = new RiskWeightedCandidateSelectionStrategy(db, riskService);

        var selected = await strategy.SelectAsync(
            DefaultContext(),
            [
                new CandidateSelectionRow(1, "MethodOne", "public void MethodOne() { }", 0.5, 2),
                new CandidateSelectionRow(2, "MethodTwo", "public void MethodTwo() { }", 0.5, 2),
                new CandidateSelectionRow(3, "MethodThree", "public void MethodThree() { }", 0.5, 2)
            ]);

        Assert.Equal(3, selected.Count);
        Assert.Equal(2, selected[0].MemberId);  // highest risk first
        Assert.Equal(1, selected[1].MemberId);
        Assert.Equal(3, selected[2].MemberId);  // lowest risk last
    }

    /// <summary>
    /// When two candidates have equal risk scores, the one with lower line coverage (more
    /// room for improvement) is ranked first.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_EqualRiskScore_TiesBrokenByLineRateAscending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedMembersAsync(db, [1, 2]);

        var riskService = new FixedRiskScoringService(new Dictionary<int, double>
        {
            [1] = 0.7,
            [2] = 0.7   // equal scores
        });
        var strategy = new RiskWeightedCandidateSelectionStrategy(db, riskService);

        var selected = await strategy.SelectAsync(
            DefaultContext(),
            [
                new CandidateSelectionRow(1, "MethodA", "public void MethodA() { }", 0.8, 1),
                new CandidateSelectionRow(2, "MethodB", "public void MethodB() { }", 0.2, 1)
            ]);

        Assert.Equal(2, selected.Count);
        Assert.Equal(2, selected[0].MemberId);  // lower line rate first (more room to improve)
    }

    /// <summary>
    /// A candidate whose member ID is not in the risk scoring result still appears
    /// in the output with a default risk score of 0.0, ranked at the end.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectAsync_CandidateWithNoRiskScore_DefaultsToZeroAndRankedLast()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedMembersAsync(db, [1, 2]);

        // Only member 1 has a score; member 2 gets no entry
        var riskService = new FixedRiskScoringService(new Dictionary<int, double> { [1] = 0.6 });
        var strategy = new RiskWeightedCandidateSelectionStrategy(db, riskService);

        var selected = await strategy.SelectAsync(
            DefaultContext(),
            [
                new CandidateSelectionRow(1, "Scored", "public void Scored() { }", 0.5, 1),
                new CandidateSelectionRow(2, "Unscored", "public void Unscored() { }", 0.5, 1)
            ]);

        Assert.Equal(2, selected.Count);
        Assert.Equal(1, selected[0].MemberId);  // scored member first
        Assert.Equal(2, selected[1].MemberId);  // unscored member last with default 0.0
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

    private static async Task SeedMembersAsync(TestMapDbContext db, IReadOnlyList<int> memberIds)
    {
        db.CSharpSolutions.Add(new CSharpSolutionEntity
        {
            Id = 1, ProjectId = 1, FilePath = "repo.sln", ContentHash = "solution"
        });
        db.Projects.Add(new ProjectEntity
        {
            Id = 1, Owner = "o", RepoName = "r", DirectoryPath = ".", ContentHash = "p"
        });
        db.CSharpProjects.Add(new CSharpProjectEntity
        {
            Id = 1, SolutionId = 1, FilePath = "src/Source.csproj",
            BuildMetadata = new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" },
            ContentHash = "project"
        });
        db.Files.Add(new FileEntity
        {
            Id = 1, CSharpProjectId = 1, FilePath = "src/Svc.cs", ContentHash = "file"
        });
        db.Objects.Add(new ObjectEntity
        {
            Id = 1, FileId = 1, Namespace = "Ns", Name = "Cls", Kind = "class",
            FullString = "public class Cls {}", ContentHash = "object"
        });
        foreach (var id in memberIds)
        {
            db.Members.Add(new MemberEntity
            {
                Id = id, ObjectEntityId = 1, Name = $"Method{id}", Kind = "method",
                Modifiers = ["public"], FullString = $"public void Method{id}() {{ }}",
                Location = new Location(id, 0, id, 0), ContentHash = $"member-{id}"
            });
        }

        await db.SaveChangesAsync();
    }

    private static CandidateSelectionContext DefaultContext() => new CandidateSelectionContext
    {
        ExperimentConfiguration = new ExperimentConfig(),
        TargetSelection = new TargetSelectionConfig { Strategy = TargetSelectionStrategy.RiskWeighted },
        SelectionTime = DateTime.UtcNow,
        EffectiveLimit = 10
    };

    /// <summary>
    /// Test double for <see cref="IRiskScoringService"/> that returns pre-set scores per member ID.
    /// </summary>
    private sealed class FixedRiskScoringService : IRiskScoringService
    {
        private readonly Dictionary<int, double> _scores;

        public FixedRiskScoringService(Dictionary<int, double> scores) => _scores = scores;

        public RiskScoringValidationResult ValidateWeights(RiskScoringRequest request)
            => RiskScoringValidationResult.Success();

        public Task<IReadOnlyList<MethodRiskScore>> ScoreAsync(
            RiskScoringRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MethodRiskScore> scores = request.CandidateMembers
                .Select(m => new MethodRiskScore
                {
                    MemberId = m.Id,
                    RiskScore = _scores.TryGetValue(m.Id, out var s) ? s : 0.0
                })
                .ToList();
            return Task.FromResult(scores);
        }
    }
}
