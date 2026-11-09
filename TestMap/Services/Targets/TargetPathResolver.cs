using TestMap.Models.Targets;

namespace TestMap.Services.Targets;

public sealed class TargetPathResolver
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public TargetPaths Resolve(
        RepositoryTarget target,
        string workspaceRoot,
        string outputRoot,
        string logRoot,
        DateTimeOffset? runStartedAtUtc = null)
    {
        if (string.Equals(Path.GetFullPath(workspaceRoot), Path.GetFullPath(outputRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Workspace and output roots must be distinct for pinned targets.");
        var parts = target.Repository.Split('/');
        if (parts.Length != 2) throw new InvalidDataException("Target repository identity must use owner/name format.");
        var owner = ValidateSegment(parts[0]);
        var repository = ValidateSegment(parts[1]);
        var revision = ValidateSegment(target.Commit);
        var relative = Path.Combine(owner, repository, revision);
        var startedAtUtc = runStartedAtUtc ?? DateTimeOffset.UtcNow;
        if (startedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Target log timestamp must be UTC.");
        var logRelative = Path.Combine(
            startedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            $"{startedAtUtc.ToString("HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture)}_{owner}-{repository}",
            "run.log");
        return new TargetPaths(
            Within(workspaceRoot, relative),
            Within(outputRoot, Path.Combine(relative, "analysis.db")),
            Within(outputRoot, Path.Combine(relative, "artifacts")),
            Within(logRoot, logRelative));
    }

    private static string ValidateSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || ReservedNames.Contains(value) ||
            ReservedNames.Contains(Path.GetFileNameWithoutExtension(value)) ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.EndsWith(' ') || value.EndsWith('.'))
            throw new InvalidDataException($"Unsafe target path segment '{value}'.");
        return value.ToLowerInvariant();
    }

    private static string Within(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Target path escapes its configured root.");
        return candidate;
    }
}
