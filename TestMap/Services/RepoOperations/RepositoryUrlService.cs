namespace TestMap.Services.RepoOperations;

public sealed class RepositoryUrlService
{
    public string NormalizeForComparison(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var redacted = RedactCredentials(value.Trim()).Replace('\\', '/').TrimEnd('/');
        if (redacted.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            redacted = redacted[..^4];
        return redacted.ToLowerInvariant();
    }

    public bool Equivalent(string left, string right) =>
        string.Equals(NormalizeForComparison(left), NormalizeForComparison(right), StringComparison.Ordinal);

    public string RedactCredentials(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
            return value;

        var builder = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty };
        return builder.Uri.AbsoluteUri;
    }
}
