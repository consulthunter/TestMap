using LibGit2Sharp;

namespace TestMap.Services.RepoOperations;

/// <summary>
/// Keeps a workspace byte-identical to its commit. Without a repository-local setting,
/// LibGit2Sharp falls back to the machine configuration (Git for Windows ships
/// <c>core.autocrlf=true</c>), so a repository without a <c>.gitattributes</c> is checked out
/// with CRLF line endings. The Linux agent containers then read every such file as modified
/// against HEAD, and the whole tree is attributed to the tool attempt.
///
/// <c>core.autocrlf=false</c> alone is not enough for a repository whose attributes mark files
/// <c>text=auto</c>: those still check out with the native eol, CRLF on Windows. An agent that
/// rewrites such a file unchanged saves it with LF, and LibGit2Sharp then reports it modified
/// in a way a forced checkout plus hard reset cannot clear, because the normalized content
/// already matches. Pinning <c>core.eol=lf</c> checks those files out as the committed LF bytes.
/// </summary>
internal static class RepositoryLineEndings
{
    private const string AutoCrlfKey = "core.autocrlf";
    private const string EolKey = "core.eol";

    /// <summary>
    /// Sets <c>core.autocrlf=false</c> and <c>core.eol=lf</c> locally. Returns true when either
    /// setting changed.
    /// </summary>
    public static bool Pin(Repository repository)
    {
        var autoCrlfChanged = PinLocal(repository, AutoCrlfKey, "false");
        var eolChanged = PinLocal(repository, EolKey, "lf");
        return autoCrlfChanged || eolChanged;
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

    /// <summary>Pins the settings and, when they changed, rewrites the tree from <paramref name="commitSha"/>.</summary>
    public static void EnsureExactCheckout(string workspacePath, string commitSha)
    {
        bool changed;
        using (var repository = new Repository(workspacePath))
        {
            changed = Pin(repository);
        }

        if (changed) RewriteWorkingTree(workspacePath, commitSha);
    }

    private static bool PinLocal(Repository repository, string key, string value)
    {
        var current = repository.Config.Get<string>(key, ConfigurationLevel.Local)?.Value;
        if (string.Equals(current, value, StringComparison.OrdinalIgnoreCase)) return false;

        repository.Config.Set(key, value, ConfigurationLevel.Local);
        return true;
    }
}
