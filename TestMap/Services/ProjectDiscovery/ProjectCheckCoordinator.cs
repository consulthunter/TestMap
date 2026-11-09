using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery.Contracts;

namespace TestMap.Services.ProjectDiscovery;

public sealed class ProjectCheckCoordinator(
    IProjectCheckProbe probe,
    ProjectCheckContractValidator validator,
    ProjectCheckSanitizer sanitizer) : IProjectCheckCoordinator
{
    public async Task<IReadOnlyList<ProjectCheckObservation>> CheckAsync(
        TargetManifest manifest,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = manifest.Targets.Select(async (target, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                try
                {
                    var observation = await probe.CheckAsync(target, cancellationToken);
                    validator.ValidateObservation(observation);
                    return (Index: index, Observation: observation);
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    var failed = new ProjectCheckObservation(
                        target.TargetId, target.Repository, target.Commit, null,
                        ProjectCheckStatus.CheckFailed, ProjectCheckPolicy.Name, ProjectCheckPolicy.Version,
                        null, null, null, "check_failed", sanitizer.Summary("Repository project check failed."),
                        DateTimeOffset.UtcNow);
                    validator.ValidateObservation(failed);
                    return (Index: index, Observation: failed);
                }
            }
            finally { semaphore.Release(); }
        });
        var results = await Task.WhenAll(tasks);
        return results.OrderBy(item => item.Index).Select(item => item.Observation).ToArray();
    }
}
