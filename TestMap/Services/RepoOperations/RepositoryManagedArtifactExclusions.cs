using LibGit2Sharp;

namespace TestMap.Services.RepoOperations;

internal static class RepositoryManagedArtifactExclusions
{
    private const string Marker = "# TestMap managed runtime artifacts";
    private static readonly string[] Patterns = ["/coverage/", "/mutation/", "**/TestResults/"];

    public static void Ensure(Repository repository)
    {
        var excludePath = Path.Combine(repository.Info.Path, "info", "exclude");
        Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
        var existing = File.Exists(excludePath) ? File.ReadAllText(excludePath) : string.Empty;
        var existingLines = existing
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        var missingPatterns = Patterns.Where(pattern => !existingLines.Contains(pattern)).ToArray();
        if (missingPatterns.Length == 0) return;

        using var writer = new StreamWriter(excludePath, append: true);
        if (existing.Length > 0 && existing[^1] is not '\r' and not '\n') writer.WriteLine();
        if (!existingLines.Contains(Marker)) writer.WriteLine(Marker);
        foreach (var pattern in missingPatterns) writer.WriteLine(pattern);
    }
}
