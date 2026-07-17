using System.Text;
using TestMap.Models.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.Targets;

public sealed class SingleRepositoryUrlParser(
    TargetIdentityService identity,
    TargetFingerprintService fingerprint) : ISingleRepositoryUrlParser
{
    public SingleRepositoryRequest Parse(string url, RepositoryAuthenticationMode authenticationMode)
    {
        if (string.IsNullOrWhiteSpace(url) || !string.Equals(url, url.Trim(), StringComparison.Ordinal))
            throw new InvalidDataException("Repository URL is required and must not contain surrounding whitespace.");
        if (url.Contains('%', StringComparison.Ordinal))
            throw new InvalidDataException("Percent-encoded repository URLs are not accepted.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidDataException("URL must be an HTTPS github.com repository URL without credentials, query, or fragment.");

        var path = uri.AbsolutePath.TrimEnd('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 || segments.Any(segment => segment is "." or ".."))
            throw new InvalidDataException("URL must identify exactly one GitHub owner/repository path.");
        var repositoryName = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];
        var rawRepository = segments[0] + "/" + repositoryName;
        if (!identity.TryNormalizeRepository(rawRepository, out var repository))
            throw new InvalidDataException("GitHub repository owner/name is invalid.");

        var urlHash = fingerprint.ComputeSha256(Encoding.UTF8.GetBytes(url));
        return new SingleRepositoryRequest(
            url,
            urlHash,
            repository,
            TargetIdentityService.CanonicalGitHubUrl(repository),
            SingleRepositoryResolutionPolicy.Provider,
            SingleRepositoryResolutionPolicy.Name,
            SingleRepositoryResolutionPolicy.Version,
            authenticationMode);
    }
}
