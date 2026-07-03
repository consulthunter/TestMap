namespace TestMap.Services.AgentTools;

public static class AgentToolChangedPathFilter
{
    private static readonly HashSet<string> ExcludedRootDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".testmap",
        ".aider",
        ".claude",
        ".codex",
        ".copilot",
        ".gemini",
        ".openhands",
        ".mini-swe-agent",
        ".swe-agent"
    };

    public static IReadOnlyList<string> Filter(IEnumerable<string> paths)
    {
        return paths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize)
            .Where(x => !string.IsNullOrEmpty(x))
            .Where(x => !IsExcluded(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsExcluded(string path)
    {
        var normalized = Normalize(path);
        if (string.IsNullOrEmpty(normalized))
            return true;

        var firstSeparator = normalized.IndexOf('/');
        var root = firstSeparator >= 0 ? normalized[..firstSeparator] : normalized;
        return ExcludedRootDirectories.Contains(root);
    }

    public static int CountIncludedDeletedFiles(string gitStatus)
    {
        if (string.IsNullOrWhiteSpace(gitStatus))
            return 0;

        return gitStatus
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Count(line =>
            {
                if (line.Length < 3 || line[0] != 'D' && line[1] != 'D')
                    return false;

                var path = line[2..].Trim();
                var renameSeparator = path.LastIndexOf(" -> ", StringComparison.Ordinal);
                if (renameSeparator >= 0)
                    path = path[(renameSeparator + 4)..];

                return !IsExcluded(path);
            });
    }

    private static string Normalize(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        if (normalized.StartsWith("/workspace/", StringComparison.Ordinal))
            normalized = normalized["/workspace/".Length..];
        else if (normalized.Equals("/workspace", StringComparison.Ordinal))
            return string.Empty;

        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        while (normalized.StartsWith("/", StringComparison.Ordinal))
            normalized = normalized[1..];

        return normalized;
    }
}
