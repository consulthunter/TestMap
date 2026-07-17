using LibGit2Sharp;
using TestMap.Models.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.Targets;

public enum RemoteProbeStatus
{
    Available,
    RepositoryUnavailable,
    AuthenticationFailed,
    RateLimited,
    CommitUnavailable,
    Failed
}

public sealed record RemoteProbeResult(RemoteProbeStatus Status, string? ResolvedCommit, string Summary);

public interface IRemoteRepositoryProbe
{
    Task<RemoteProbeResult> ProbeAsync(RepositoryTarget target, CancellationToken cancellationToken = default);
}

public sealed class LibGitRemoteRepositoryProbe : IRemoteRepositoryProbe
{
    public Task<RemoteProbeResult> ProbeAsync(RepositoryTarget target, CancellationToken cancellationToken = default) =>
        Task.Run(() => Probe(target, cancellationToken), cancellationToken);

    private static RemoteProbeResult Probe(RepositoryTarget target, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-verify-" + Guid.NewGuid().ToString("N"));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Repository.Clone(target.Url, directory, new CloneOptions { IsBare = true });
            using var repository = new Repository(directory);
            var commit = repository.Lookup<Commit>(target.Commit);
            return commit is null
                ? new RemoteProbeResult(RemoteProbeStatus.CommitUnavailable, null, "Requested commit is not available from the repository.")
                : new RemoteProbeResult(RemoteProbeStatus.Available, commit.Sha.ToLowerInvariant(), "Requested commit is available.");
        }
        catch (LibGit2SharpException exception)
        {
            var message = exception.Message.ToLowerInvariant();
            if (message.Contains("authentication") || message.Contains("credentials") || message.Contains("401") || message.Contains("403"))
                return new RemoteProbeResult(RemoteProbeStatus.AuthenticationFailed, null, "Repository authentication failed.");
            if (message.Contains("rate limit") || message.Contains("429"))
                return new RemoteProbeResult(RemoteProbeStatus.RateLimited, null, "Repository verification was rate limited.");
            return new RemoteProbeResult(RemoteProbeStatus.RepositoryUnavailable, null, "Repository is unavailable.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new RemoteProbeResult(RemoteProbeStatus.Failed, null, "Repository verification failed.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

public sealed class TargetVerificationService(IRemoteRepositoryProbe probe) : ITargetVerificationService
{
    public async Task<TargetVerificationRecord> VerifyAsync(
        RepositoryTarget target,
        string manifestSha256,
        CancellationToken cancellationToken = default)
    {
        RemoteProbeResult result;
        try
        {
            result = await probe.ProbeAsync(target, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            result = new RemoteProbeResult(RemoteProbeStatus.Failed, null, "Repository verification failed.");
        }

        var status = result.Status switch
        {
            RemoteProbeStatus.Available when string.Equals(result.ResolvedCommit, target.Commit, StringComparison.OrdinalIgnoreCase) => TargetVerificationStatus.Available,
            RemoteProbeStatus.Available => TargetVerificationStatus.VerificationFailed,
            RemoteProbeStatus.RepositoryUnavailable => TargetVerificationStatus.RepositoryUnavailable,
            RemoteProbeStatus.AuthenticationFailed => TargetVerificationStatus.AuthenticationFailed,
            RemoteProbeStatus.RateLimited => TargetVerificationStatus.RateLimited,
            RemoteProbeStatus.CommitUnavailable => TargetVerificationStatus.CommitUnavailable,
            _ => TargetVerificationStatus.VerificationFailed
        };
        var resolved = status == TargetVerificationStatus.Available ? result.ResolvedCommit?.ToLowerInvariant() : null;
        return new TargetVerificationRecord(
            "1.0", manifestSha256, target.TargetId, target.Repository, target.Commit, resolved,
            status, DateTimeOffset.UtcNow, result.Summary);
    }
}
