namespace TestMap.Services.Experiment.Execution;

/// <summary>
/// The one targeted baseline a candidate gets per resume group. Both evaluation lanes compare
/// against it, and a resumed run reuses it instead of measuring the candidate again.
/// </summary>
public sealed record CandidateBaselineRecord(
    string StableKey,
    int ExperimentRunId,
    int CandidateMethodId,
    int MemberId,
    int? TestRunId,
    bool Usable,
    string FailureReason);

public interface ICandidateBaselineStore
{
    Task<CandidateBaselineRecord?> GetAsync(string stableKey, CancellationToken cancellationToken = default);
    Task SaveAsync(CandidateBaselineRecord record, CancellationToken cancellationToken = default);
}

/// <summary>Keeps baselines for the lifetime of one orchestrator; used when no database store is wired.</summary>
public sealed class InMemoryCandidateBaselineStore : ICandidateBaselineStore
{
    private readonly Dictionary<string, CandidateBaselineRecord> _records = new(StringComparer.Ordinal);

    public Task<CandidateBaselineRecord?> GetAsync(string stableKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_records.GetValueOrDefault(stableKey));

    public Task SaveAsync(CandidateBaselineRecord record, CancellationToken cancellationToken = default)
    {
        _records[record.StableKey] = record;
        return Task.CompletedTask;
    }
}
