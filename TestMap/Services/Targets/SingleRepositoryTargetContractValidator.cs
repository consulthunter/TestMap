using System.Text;
using TestMap.Models.Targets;

namespace TestMap.Services.Targets;

public sealed class SingleRepositoryTargetContractValidator(
    SingleRepositoryResolutionValidator resolutionValidator,
    TargetFingerprintService fingerprint)
{
    public void Validate(TargetManifest manifest, SingleRepositoryResolution resolution)
    {
        resolutionValidator.Validate(resolution);
        if (resolution.Status != SingleRepositoryResolutionStatus.Resolved || manifest.SchemaVersion != 3 ||
            manifest.Resolution is null || manifest.Targets.Count != 1)
            throw new InvalidDataException("URL-created target requires one resolved schema-3 target.");
        if (manifest.Source.Kind != "github_repository_url" ||
            manifest.Source.Url != resolution.RequestedUrl ||
            manifest.Source.Sha256 != resolution.RequestedUrlSha256 ||
            fingerprint.ComputeSha256(Encoding.UTF8.GetBytes(manifest.Source.Url)) != manifest.Source.Sha256)
            throw new InvalidDataException("Schema-3 URL source provenance differs from resolution.");
        var target = manifest.Targets[0];
        if (target.Repository != resolution.Repository ||
            target.Url != resolution.CanonicalUrl ||
            target.Commit != resolution.ResolvedCommit ||
            target.TargetId != TargetIdentityService.CreateTargetId(target.Repository, target.Commit) ||
            !target.SourceRows.SequenceEqual([1]))
            throw new InvalidDataException("Schema-3 target identity differs from resolution provenance.");
    }
}
