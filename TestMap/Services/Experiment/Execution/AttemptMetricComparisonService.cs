using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef;

namespace TestMap.Services.Experiment.Execution;

public interface IAttemptMetricComparisonService
{
    Task<AttemptMetricComparison> CompareAsync(
        int targetMemberId,
        int? baselineTestRunId,
        int? postTestRunId,
        CancellationToken cancellationToken = default);
}

public sealed class AttemptMetricComparisonService(TestMapDbContext dbContext)
    : IAttemptMetricComparisonService
{
    public async Task<AttemptMetricComparison> CompareAsync(
        int targetMemberId,
        int? baselineTestRunId,
        int? postTestRunId,
        CancellationToken cancellationToken = default)
    {
        var coverageBefore = await GetMemberCoverageAsync(baselineTestRunId, targetMemberId, cancellationToken);
        var coverageAfter = await GetMemberCoverageAsync(postTestRunId, targetMemberId, cancellationToken);
        var baselineMutation = await GetMutationAsync(baselineTestRunId, cancellationToken);
        var postMutation = await GetMutationAsync(postTestRunId, cancellationToken);

        var coverageStatus = CoverageStatus(coverageBefore, coverageAfter);
        var coverageComparable = coverageStatus == "Paired";
        var mutationStatus = MutationStatus(baselineMutation, postMutation);
        var mutationComparable = mutationStatus == "Paired";

        return new AttemptMetricComparison(
            coverageBefore?.LineRate,
            coverageAfter?.LineRate,
            coverageComparable ? Difference(coverageBefore?.LineRate, coverageAfter?.LineRate) : null,
            coverageStatus,
            baselineMutation?.Score,
            postMutation?.Score,
            mutationComparable ? postMutation!.Score - baselineMutation!.Score : null,
            mutationStatus,
            BuildImpactStatus(coverageStatus, mutationStatus),
            BuildReason(coverageStatus, mutationStatus));
    }

    private async Task<CoverageObservation?> GetMemberCoverageAsync(
        int? testRunId,
        int memberId,
        CancellationToken cancellationToken)
    {
        if (!testRunId.HasValue || memberId <= 0) return null;

        return await (
            from report in dbContext.CoverageReports
            join coverage in dbContext.MemberCoverages on report.Id equals coverage.CoverageReportId
            where report.TestRunId == testRunId.Value
                  && report.MeasurementPolicyVersion == "coverage-integrity-v1"
                  && report.HasUsableCoverage
                  && coverage.MemberId == memberId
                  && coverage.AttributionStatus == "Mapped"
            orderby report.Id descending, coverage.Id descending
            select new CoverageObservation(
                coverage.LineRate,
                report.ScopeKind,
                report.SourceProjectPath,
                report.TestProjectPath,
                report.TargetFramework)
        ).FirstOrDefaultAsync(cancellationToken);
    }

    private static string CoverageStatus(CoverageObservation? before, CoverageObservation? after)
    {
        var pairStatus = PairStatus(before, after);
        if (pairStatus != "Paired") return pairStatus;
        return SameScope(before!, after!) ? "Paired" : "ScopeMismatch";
    }

    private async Task<MutationObservation?> GetMutationAsync(
        int? testRunId,
        CancellationToken cancellationToken)
    {
        if (!testRunId.HasValue) return null;

        return await dbContext.MutationTestingReports
            .Where(x => x.TestRunId == testRunId.Value)
            .OrderByDescending(x => x.Id)
            .Select(x => new MutationObservation(
                x.MutationScore,
                x.ScopeKind,
                x.SourceProjectPath,
                x.TestProjectPath,
                x.TargetFramework))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string MutationStatus(MutationObservation? before, MutationObservation? after)
    {
        var pairStatus = PairStatus(before, after);
        if (pairStatus != "Paired") return pairStatus;
        return SameScope(before!, after!) ? "Paired" : "ScopeMismatch";
    }

    private static bool SameScope(MutationObservation before, MutationObservation after) =>
        string.Equals(before.ScopeKind, after.ScopeKind, StringComparison.OrdinalIgnoreCase) &&
        SamePath(before.SourceProjectPath, after.SourceProjectPath) &&
        SamePath(before.TestProjectPath, after.TestProjectPath) &&
        string.Equals(before.TargetFramework, after.TargetFramework, StringComparison.OrdinalIgnoreCase);

    private static bool SameScope(CoverageObservation before, CoverageObservation after) =>
        string.Equals(before.ScopeKind, after.ScopeKind, StringComparison.OrdinalIgnoreCase) &&
        SamePath(before.SourceProjectPath, after.SourceProjectPath) &&
        SamePath(before.TestProjectPath, after.TestProjectPath) &&
        string.Equals(before.TargetFramework, after.TargetFramework, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string PairStatus<T>(T? before, T? after) where T : class =>
        before == null && after == null ? "MissingBoth" :
        before == null ? "MissingBaseline" :
        after == null ? "MissingPost" : "Paired";

    private static string PairStatus(double? before, double? after) =>
        !before.HasValue && !after.HasValue ? "MissingBoth" :
        !before.HasValue ? "MissingBaseline" :
        !after.HasValue ? "MissingPost" : "Paired";

    private static double? Difference(double? before, double? after) =>
        before.HasValue && after.HasValue ? after.Value - before.Value : null;

    private static string BuildImpactStatus(string coverageStatus, string mutationStatus) =>
        coverageStatus == "Paired" && mutationStatus == "Paired" ? "Complete" :
        coverageStatus == "Paired" || mutationStatus == "Paired" ? "Partial" : "Missing";

    private static string BuildReason(string coverageStatus, string mutationStatus) =>
        coverageStatus == "Paired" && mutationStatus == "Paired"
            ? string.Empty
            : $"Coverage={coverageStatus}; Mutation={mutationStatus}";

    private sealed record MutationObservation(
        double Score,
        string ScopeKind,
        string SourceProjectPath,
        string TestProjectPath,
        string TargetFramework);

    private sealed record CoverageObservation(
        double LineRate,
        string ScopeKind,
        string SourceProjectPath,
        string TestProjectPath,
        string TargetFramework);
}

public sealed record AttemptMetricComparison(
    double? CoverageBefore,
    double? CoverageAfter,
    double? CoverageDelta,
    string CoverageStatus,
    double? MutationScoreBefore,
    double? MutationScoreAfter,
    double? MutationScoreDelta,
    string MutationStatus,
    string ImpactStatus,
    string FailureReason);
