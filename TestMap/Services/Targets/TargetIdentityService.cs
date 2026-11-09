using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TestMap.Models.Targets;

namespace TestMap.Services.Targets;

public sealed partial class TargetIdentityService
{
    [GeneratedRegex("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex FullShaRegex();

    [GeneratedRegex("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryRegex();

    public bool TryNormalizeRepository(string? value, out string repository)
    {
        var candidate = value?.Trim().TrimEnd('/');
        if (candidate?.EndsWith(".git", StringComparison.OrdinalIgnoreCase) == true)
            candidate = candidate[..^4];
        if (candidate?.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) == true)
            candidate = candidate["https://github.com/".Length..];

        if (string.IsNullOrWhiteSpace(candidate) || !RepositoryRegex().IsMatch(candidate) || candidate.Contains("..", StringComparison.Ordinal))
        {
            repository = string.Empty;
            return false;
        }

        repository = candidate.ToLowerInvariant();
        return true;
    }

    public bool TryNormalizeCommit(string? value, out string commit)
    {
        var candidate = value?.Trim();
        if (candidate is null || !FullShaRegex().IsMatch(candidate) || candidate.All(character => character == '0'))
        {
            commit = string.Empty;
            return false;
        }

        commit = candidate.ToLowerInvariant();
        return true;
    }

    public static string CanonicalGitHubUrl(string repository) => $"https://github.com/{repository}.git";

    public static string CreateTargetId(string repository, string commit)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{repository.ToLowerInvariant()}|{commit.ToLowerInvariant()}"));
        return Convert.ToHexStringLower(bytes);
    }

    public RepositoryTarget Create(string repository, string commit, IReadOnlyList<int> sourceRows)
    {
        if (!TryNormalizeRepository(repository, out var normalizedRepository))
            throw new ArgumentException("Repository must be an owner/name identity.", nameof(repository));
        if (!TryNormalizeCommit(commit, out var normalizedCommit))
            throw new ArgumentException("Commit must be a full 40-character SHA.", nameof(commit));

        return new RepositoryTarget(
            CreateTargetId(normalizedRepository, normalizedCommit),
            normalizedRepository,
            CanonicalGitHubUrl(normalizedRepository),
            normalizedCommit,
            sourceRows.Distinct().Order().ToArray());
    }
}
