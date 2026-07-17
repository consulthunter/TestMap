namespace TestMap.Services.ProjectDiscovery;

public sealed partial class ProjectCheckSanitizer
{
    private const int MaxSummaryLength = 300;

    public string Summary(string value)
    {
        var sanitized = (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        sanitized = CredentialPattern().Replace(sanitized, "[redacted]");
        return sanitized.Length <= MaxSummaryLength ? sanitized : sanitized[..MaxSummaryLength];
    }

    public string EvidencePath(string value)
    {
        var path = (value ?? string.Empty).Replace('\\', '/').Trim('/');
        if (path.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(path))
            throw new InvalidDataException("Evidence path must be repository-relative.");
        return path.Length <= 1024 ? path : path[..1024];
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"(?i)(authorization\s*[:=]\s*(?:bearer\s+)?\S+|bearer\s+\S+|(?:api[_ -]?key|token|password)\s*[:=]\s*\S+|ghp_[A-Za-z0-9]+|github_pat_[A-Za-z0-9_]+)",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex CredentialPattern();
}
