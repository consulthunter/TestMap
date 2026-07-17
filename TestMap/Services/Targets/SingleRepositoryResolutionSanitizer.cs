using System.Text.RegularExpressions;

namespace TestMap.Services.Targets;

public sealed partial class SingleRepositoryResolutionSanitizer
{
    private const int MaxSummaryLength = 300;

    public string Sanitize(string value)
    {
        var sanitized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        sanitized = CredentialPattern().Replace(sanitized, "[redacted]");
        return sanitized.Length <= MaxSummaryLength ? sanitized : sanitized[..MaxSummaryLength];
    }

    [GeneratedRegex(
        @"(?i)(authorization\s*[:=]\s*(?:bearer\s+)?\S+|bearer\s+\S+|(?:api[_ -]?key|token|password)\s*[:=]\s*\S+|ghp_[A-Za-z0-9]+|github_pat_[A-Za-z0-9_]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex CredentialPattern();
}
