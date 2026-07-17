using System.Globalization;
using System.Text.RegularExpressions;

namespace TestMap.Services.Logging;

public sealed record ProjectLogDirectoryAllocation(
    DateTimeOffset RunStartedAtUtc,
    string DirectoryPath,
    string ReservationMarkerPath,
    string LogFilePath,
    int Ordinal);

public sealed partial class ProjectLogDirectoryAllocator
{
    public const string ReservationMarkerFileName = ".testmap-log-reservation";

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public ProjectLogDirectoryAllocation Allocate(
        string logRoot,
        DateTimeOffset runStartedAtUtc,
        string owner,
        string repository,
        string projectId,
        string? logFileName = null)
    {
        if (string.IsNullOrWhiteSpace(logRoot))
            throw new ArgumentException("A configured log root is required.", nameof(logRoot));
        if (runStartedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("The log run timestamp must be UTC.", nameof(runStartedAtUtc));

        var normalizedOwner = ValidateSegment(owner, nameof(owner));
        var normalizedRepository = ValidateSegment(repository, nameof(repository));
        var safeProjectId = ValidateSegment(projectId, nameof(projectId), normalizeCase: false);
        var safeLogFileName = string.IsNullOrWhiteSpace(logFileName)
            ? safeProjectId + ".log"
            : ValidateSegment(logFileName, nameof(logFileName), normalizeCase: false);
        var fullRoot = Path.GetFullPath(logRoot);
        var dateDirectory = Within(fullRoot, runStartedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dateDirectory);

        var timeSegment = runStartedAtUtc.ToString("HH-mm-ss", CultureInfo.InvariantCulture);
        var baseLeaf = $"{timeSegment}_{normalizedOwner}-{normalizedRepository}";
        for (var ordinal = 1; ; ordinal++)
        {
            var suffix = ordinal == 1
                ? string.Empty
                : "-" + ordinal.ToString("D2", CultureInfo.InvariantCulture);
            var directory = Within(fullRoot, Path.Combine(
                runStartedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                baseLeaf + suffix));
            if (Directory.Exists(directory)) continue;

            Directory.CreateDirectory(directory);
            var markerPath = Path.Combine(directory, ReservationMarkerFileName);
            try
            {
                using var marker = new FileStream(
                    markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 1, FileOptions.WriteThrough);
                marker.Flush(flushToDisk: true);
            }
            catch (IOException) when (File.Exists(markerPath))
            {
                continue;
            }

            return new ProjectLogDirectoryAllocation(
                runStartedAtUtc,
                directory,
                markerPath,
                Path.Combine(directory, safeLogFileName),
                ordinal);
        }
    }

    private static string ValidateSegment(string value, string parameterName, bool normalizeCase = true)
    {
        var segment = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(segment) || segment is "." or ".." ||
            ReservedNames.Contains(segment) || ReservedNames.Contains(Path.GetFileNameWithoutExtension(segment)) ||
            !SafeSegmentRegex().IsMatch(segment) || segment.Contains("..", StringComparison.Ordinal) ||
            segment.EndsWith(' ') || segment.EndsWith('.'))
            throw new ArgumentException("Log path identity contains an unsafe segment.", parameterName);
        return normalizeCase ? segment.ToLowerInvariant() : segment;
    }

    private static string Within(string root, string relative)
    {
        var rooted = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(rooted, relative));
        if (!candidate.StartsWith(rooted, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Log directory escapes the configured root.");
        return candidate;
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSegmentRegex();
}
