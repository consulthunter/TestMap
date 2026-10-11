using LibGit2Sharp;

namespace TestMap.Services.RepoOperations;

/// <summary>
/// Keeps a workspace byte-identical to its commit. Without a repository-local setting,
/// LibGit2Sharp falls back to the machine configuration (Git for Windows ships
/// <c>core.autocrlf=true</c>), so a repository without a <c>.gitattributes</c> is checked out
/// with CRLF line endings. The Linux agent containers then read every such file as modified
/// against HEAD, and the whole tree is attributed to the tool attempt.
/// </summary>
internal static class RepositoryLineEndings
{
    private const string AutoCrlfKey = "core.autocrlf";

    /// <summary>Sets <c>core.autocrlf=false</c> locally. Returns true when the setting changed.</summary>
    public static bool Pin(Repository repository)
    {
        var current = repository.Config.Get<string>(AutoCrlfKey, ConfigurationLevel.Local)?.Value;
        if (string.Equals(current, "false", StringComparison.OrdinalIgnoreCase)) return false;

        repository.Config.Set(AutoCrlfKey, "false", ConfigurationLevel.Local);
        return true;
    }

    /// <summary>
    /// Rewrites every tracked file from <paramref name="commitSha"/>. Needed after <see cref="Pin"/>
    /// changes the setting: the index stat cache still matches the converted files, so a forced
    /// checkout alone would leave them in place.
    /// </summary>
    public static void RewriteWorkingTree(string workspacePath, string commitSha)
    {
        using (var repository = new Repository(workspacePath))
        {
            var indexPath = Path.Combine(repository.Info.Path, "index");
            if (File.Exists(indexPath)) File.Delete(indexPath);
        }

        using var rewritten = new Repository(workspacePath);
        var commit = rewritten.Lookup<Commit>(commitSha) ??
                     throw new InvalidOperationException($"Commit {commitSha} is unavailable locally.");
        rewritten.Reset(ResetMode.Hard, commit);
    }

    /// <summary>Pins the setting and, when it changed, rewrites the tree from <paramref name="commitSha"/>.</summary>
    public static void EnsureExactCheckout(string workspacePath, string commitSha)
    {
        bool changed;
        using (var repository = new Repository(workspacePath))
        {
            changed = Pin(repository);
        }

        if (changed) RewriteWorkingTree(workspacePath, commitSha);
    }
}
