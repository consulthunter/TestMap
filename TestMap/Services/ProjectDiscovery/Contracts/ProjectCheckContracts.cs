using TestMap.Models.Targets;

namespace TestMap.Services.ProjectDiscovery.Contracts;

public interface IProjectTreeClient
{
    Task<ProjectRepositoryInfo> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default);
    Task<ProjectCommitInfo> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default);
    Task<ProjectTreeResult> GetTreeAsync(string owner, string repository, string treeSha, CancellationToken cancellationToken = default);
}

public interface IProjectTestPresencePolicy
{
    string Name { get; }
    string Version { get; }
    ProjectCheckEvidence? FindEvidence(IEnumerable<string> paths);
}

public interface IProjectCheckProbe
{
    Task<ProjectCheckObservation> CheckAsync(RepositoryTarget target, CancellationToken cancellationToken = default);
}

public interface IProjectCheckCoordinator
{
    Task<IReadOnlyList<ProjectCheckObservation>> CheckAsync(
        TargetManifest manifest,
        int maxConcurrency,
        CancellationToken cancellationToken = default);
}

public interface IProjectCheckSerializer
{
    byte[] SerializeReport(ProjectCheckReport report);
    ProjectCheckReport DeserializeReport(ReadOnlySpan<byte> yaml);
    byte[] SerializeBundle(ProjectCheckBundle bundle);
    ProjectCheckBundle DeserializeBundle(ReadOnlySpan<byte> yaml);
}
