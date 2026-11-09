using TestMap.Models.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.Targets;

public sealed class TargetVerificationCoordinator(ITargetVerificationService verifier)
{
    public async Task<IReadOnlyList<TargetVerificationRecord>> VerifyAsync(
        TargetManifest manifest,
        string manifestSha256,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = manifest.Targets.Select(async (target, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try { return (index, Record: await verifier.VerifyAsync(target, manifestSha256, cancellationToken)); }
            finally { semaphore.Release(); }
        });
        var results = await Task.WhenAll(tasks);
        return results.OrderBy(item => item.index).Select(item => item.Record).ToArray();
    }
}
