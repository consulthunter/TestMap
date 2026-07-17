using System.Text;
using System.Text.RegularExpressions;
using TestMap.Models.Targets;

namespace TestMap.Services.Targets;

public sealed class SingleRepositoryResolutionValidator(TargetFingerprintService fingerprint)
{
    public void Validate(SingleRepositoryResolution resolution)
    {
        if (resolution.ResolutionSchemaVersion != 1)
            throw new InvalidDataException("Only repository resolution schema version 1 is supported.");
        if (!Enum.IsDefined(resolution.AuthenticationMode) || !Enum.IsDefined(resolution.Status))
            throw new InvalidDataException("Resolution authentication mode or status is unsupported.");
        if (resolution.RequestedAtUtc.Offset != TimeSpan.Zero || resolution.CompletedAtUtc.Offset != TimeSpan.Zero ||
            resolution.CompletedAtUtc < resolution.RequestedAtUtc)
            throw new InvalidDataException("Resolution timestamps must be ordered UTC values.");
        if (resolution.RetryAfterUtc is { } retryAfterUtc && retryAfterUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException("retry_after_utc must be UTC when present.");
        if (resolution.Provider != SingleRepositoryResolutionPolicy.Provider ||
            resolution.PolicyName != SingleRepositoryResolutionPolicy.Name ||
            resolution.PolicyVersion != SingleRepositoryResolutionPolicy.Version)
            throw new InvalidDataException("Repository resolution policy is unsupported.");
        if (resolution.ResolutionPasses is not (1 or 2))
            throw new InvalidDataException("Resolution passes must be one or two.");
        if (fingerprint.ComputeSha256(Encoding.UTF8.GetBytes(resolution.RequestedUrl)) != resolution.RequestedUrlSha256)
            throw new InvalidDataException("Requested URL hash does not match the exact URL.");
        var identity = new TargetIdentityService();
        if (!identity.TryNormalizeRepository(resolution.Repository, out var repository) || repository != resolution.Repository ||
            resolution.CanonicalUrl != TargetIdentityService.CanonicalGitHubUrl(repository))
            throw new InvalidDataException("Resolution repository identity is invalid.");
        var parsedRequest = new SingleRepositoryUrlParser(identity, fingerprint)
            .Parse(resolution.RequestedUrl, resolution.AuthenticationMode);
        if (parsedRequest.Repository != resolution.Repository ||
            parsedRequest.CanonicalUrl != resolution.CanonicalUrl ||
            parsedRequest.RequestedUrlSha256 != resolution.RequestedUrlSha256)
            throw new InvalidDataException("Requested URL does not identify the recorded repository.");
        if (string.IsNullOrWhiteSpace(resolution.Summary))
            throw new InvalidDataException("Resolution summary is required.");

        if (resolution.Status == SingleRepositoryResolutionStatus.Resolved)
        {
            if (resolution.ResolvedRepository != resolution.Repository ||
                string.IsNullOrWhiteSpace(resolution.DefaultBranch) ||
                !identity.TryNormalizeCommit(resolution.ResolvedCommit, out var commit) || commit != resolution.ResolvedCommit ||
                resolution.ReasonKind is not null || resolution.RetryAfterUtc is not null)
                throw new InvalidDataException("Resolved observation fields are incomplete or inconsistent.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(resolution.ReasonKind))
                throw new InvalidDataException("Failed resolution requires a reason_kind.");
            if (!Regex.IsMatch(resolution.ReasonKind, "^[a-z0-9_]+$", RegexOptions.CultureInvariant))
                throw new InvalidDataException("Failed resolution reason_kind must use stable snake_case vocabulary.");
            if (resolution.RetryAfterUtc is not null && resolution.Status != SingleRepositoryResolutionStatus.RateLimited)
                throw new InvalidDataException("Only rate-limited resolution may include retry_after_utc.");
        }

        if (!Regex.IsMatch(resolution.RequestedUrlSha256, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Requested URL hash must be a lower-case SHA-256 value.");
    }
}
