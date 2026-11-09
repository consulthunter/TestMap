using TestMap.Models.Targets;

namespace TestMap.Services.Targets.Contracts;

public interface ISingleRepositoryUrlParser
{
    SingleRepositoryRequest Parse(string url, RepositoryAuthenticationMode authenticationMode);
}

public interface IRepositoryResolutionClient
{
    Task<RepositoryResolutionMetadata> GetRepositoryAsync(
        string owner, string repository, CancellationToken cancellationToken = default);
    Task<RepositoryResolutionBranch> GetBranchAsync(
        string owner, string repository, string branch, CancellationToken cancellationToken = default);
    Task<RepositoryResolutionCommit> GetCommitAsync(
        string owner, string repository, string commit, CancellationToken cancellationToken = default);
}

public interface ISingleRepositoryResolver
{
    Task<SingleRepositoryResolution> ResolveAsync(
        SingleRepositoryRequest request, CancellationToken cancellationToken = default);
}

public interface ISingleRepositoryResolutionSerializer
{
    byte[] Serialize(SingleRepositoryResolution resolution);
    SingleRepositoryResolution Deserialize(ReadOnlySpan<byte> yaml);
}
