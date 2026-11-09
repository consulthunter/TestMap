using TestMap.Models.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.Targets;

public sealed class SingleRepositoryTargetCreationService(
    ISingleRepositoryUrlParser parser,
    ISingleRepositoryResolver resolver,
    ISingleRepositoryResolutionSerializer resolutionSerializer,
    ITargetManifestSerializer manifestSerializer,
    TargetIdentityService identity,
    TargetFingerprintService fingerprint,
    SingleRepositoryTargetContractValidator contractValidator,
    SingleRepositoryTargetPathResolver pathResolver,
    IAtomicFilePublisher publisher)
{
    public async Task<SingleRepositoryTargetCreationResult> CreateAsync(
        string url,
        string? outputPath,
        string? resolutionOutputPath,
        RepositoryAuthenticationMode authenticationMode,
        CancellationToken cancellationToken = default)
    {
        var request = parser.Parse(url, authenticationMode);
        var paths = pathResolver.Resolve(request, outputPath, resolutionOutputPath);
        var resolution = await resolver.ResolveAsync(request, cancellationToken);
        var resolutionBytes = resolutionSerializer.Serialize(resolution);
        var resolutionHash = fingerprint.ComputeSha256(resolutionBytes);
        var resolutionPath = SingleRepositoryTargetPathResolver.ContentAddressedResolutionPath(
            paths.ResolutionBasePath, resolutionHash);
        if (string.Equals(paths.ManifestPath, resolutionPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Manifest and content-addressed resolution paths must be distinct.");

        await publisher.PublishAsync(resolutionPath, resolutionBytes, cancellationToken);
        var publishedHash = await fingerprint.ComputeFileSha256Async(resolutionPath, cancellationToken);
        if (publishedHash != resolutionHash)
            throw new IOException("Published repository resolution hash does not match its content.");

        if (resolution.Status != SingleRepositoryResolutionStatus.Resolved)
            throw new SingleRepositoryTargetCreationException(resolution, resolutionPath);

        var target = identity.Create(resolution.Repository, resolution.ResolvedCommit!, [1]);
        var manifest = new TargetManifest(
            3,
            resolution.CompletedAtUtc,
            new TargetSourceProvenance(
                null,
                resolution.RequestedUrlSha256,
                "github_repository_url",
                resolution.RequestedUrl),
            null,
            [target],
            null,
            new TargetReportReference(Path.GetFileName(resolutionPath), resolutionHash));
        contractValidator.Validate(manifest, resolution);
        var manifestBytes = manifestSerializer.Serialize(manifest);
        await publisher.PublishAsync(paths.ManifestPath, manifestBytes, cancellationToken);
        return new SingleRepositoryTargetCreationResult(manifest, paths.ManifestPath, resolution, resolutionPath);
    }
}
