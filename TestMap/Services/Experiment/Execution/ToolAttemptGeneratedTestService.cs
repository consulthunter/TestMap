using Microsoft.EntityFrameworkCore;
using TestMap.Models.AgentTools;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.AgentTools;

namespace TestMap.Services.Experiment.Execution;

public sealed class ToolAttemptGeneratedTestLinkResult
{
    public int LinkedCount { get; init; }
    public IReadOnlyList<int> LinkedMemberIds { get; init; } = [];
}

public interface IToolAttemptGeneratedTestService
{
    /// <summary>
    /// Links test members found in <paramref name="changedFiles"/> to the given
    /// <paramref name="attempt"/>. Changed files should be relative paths from the
    /// workspace root (as written by the agent tool to <c>changed-files.txt</c>).
    /// </summary>
    Task<ToolAttemptGeneratedTestLinkResult> LinkAsync(
        ToolAttempt attempt,
        IReadOnlyList<string> changedFiles,
        int projectId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// After a completed tool attempt with changes, queries the database for test members
/// in the changed files and inserts <c>tool_attempt_generated_tests</c> linking rows.
/// </summary>
public sealed class ToolAttemptGeneratedTestService : IToolAttemptGeneratedTestService
{
    private readonly TestMapDbContext _dbContext;
    private readonly ToolAttemptGeneratedTestRepository _repo;

    public ToolAttemptGeneratedTestService(
        TestMapDbContext dbContext,
        ToolAttemptGeneratedTestRepository repo)
    {
        _dbContext = dbContext;
        _repo = repo;
    }

    public async Task<ToolAttemptGeneratedTestLinkResult> LinkAsync(
        ToolAttempt attempt,
        IReadOnlyList<string> changedFiles,
        int projectId,
        CancellationToken cancellationToken = default)
    {
        if (changedFiles.Count == 0 || string.IsNullOrWhiteSpace(attempt.WorkspacePath))
            return new ToolAttemptGeneratedTestLinkResult();

        var addedLinesByPath = ReadAddedLinesByPath(attempt);
        if (addedLinesByPath.Count == 0)
            return new ToolAttemptGeneratedTestLinkResult();

        // Build the set of absolute host paths for files changed by the tool.
        var absolutePaths = BuildAbsolutePaths(attempt.WorkspacePath, changedFiles);
        if (absolutePaths.Count == 0)
            return new ToolAttemptGeneratedTestLinkResult();

        // Find test members whose declaration starts on a line added by the tool.
        // Merely belonging to a changed file is insufficient because that file can
        // contain pre-existing tests that must not be attributed to the attempt.
        // Join path: MemberEntity → ObjectEntity → FileEntity
        var candidateMembers = await (
            from member in _dbContext.Members.AsNoTracking()
            join obj in _dbContext.Objects on member.ObjectEntityId equals obj.Id
            join file in _dbContext.Files on obj.FileId equals file.Id
            where member.IsTestMember
                  && member.Kind == "method"
                  && absolutePaths.Contains(file.FilePath)
            select new
            {
                member.Id,
                member.Location,
                file.FilePath
            }
        ).ToListAsync(cancellationToken);
        var memberIds = candidateMembers
            .Where(x =>
                addedLinesByPath.TryGetValue(Path.GetFullPath(x.FilePath), out var addedLines)
                && addedLines.Contains(x.Location.StartLineNumber))
            .Select(x => x.Id)
            .ToList();

        if (memberIds.Count == 0)
            return new ToolAttemptGeneratedTestLinkResult();

        // Look up existing source-test mappings for these test members so we can
        // optionally record which candidate they map to.
        var mappingByMemberId = await (
            from mapping in _dbContext.SourceTestMappings
            where memberIds.Contains(mapping.TestMemberId)
            orderby mapping.Confidence descending
            select new { mapping.TestMemberId, MappingId = mapping.Id }
        ).ToDictionaryAsync(x => x.TestMemberId, x => x.MappingId, cancellationToken);

        var rows = memberIds.Select(memberId => new ToolAttemptGeneratedTest
        {
            ToolAttemptId = attempt.Id,
            MemberId = memberId,
            MappingId = mappingByMemberId.TryGetValue(memberId, out var mid) ? mid : null
        }).ToList();

        await _repo.InsertManyAsync(rows, cancellationToken);

        return new ToolAttemptGeneratedTestLinkResult
        {
            LinkedCount = rows.Count,
            LinkedMemberIds = rows.Select(r => r.MemberId).ToList()
        };
    }

    /// <summary>
    /// Builds a normalised set of absolute file paths from workspace-relative changed-file paths.
    /// Handles both relative paths and container-absolute paths starting with <c>/workspace/</c>.
    /// </summary>
    internal static HashSet<string> BuildAbsolutePaths(
        string workspacePath,
        IReadOnlyList<string> changedFiles)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in changedFiles)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var relative = raw.Trim();

            // Strip container-absolute prefix written by tools inside /workspace.
            if (relative.StartsWith("/workspace/", StringComparison.Ordinal))
                relative = relative["/workspace/".Length..];
            else if (relative.Equals("/workspace", StringComparison.Ordinal))
                continue; // workspace root itself — not a file

            // Normalise path separators before joining.
            relative = relative.Replace('/', Path.DirectorySeparatorChar)
                               .Replace('\\', Path.DirectorySeparatorChar);

            try
            {
                var absolute = Path.GetFullPath(Path.Combine(workspacePath, relative));
                result.Add(absolute);
            }
            catch (ArgumentException)
            {
                // Ignore malformed paths.
            }
        }

        return result;
    }

    internal static Dictionary<string, HashSet<int>> ReadAddedLinesByPath(ToolAttempt attempt)
    {
        if (string.IsNullOrWhiteSpace(attempt.ArtifactPath)
            || string.IsNullOrWhiteSpace(attempt.WorkspacePath))
            return new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        var patchPath = Path.Combine(attempt.ArtifactPath, "patch.diff");
        if (!File.Exists(patchPath))
            return new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        return ParseAddedLines(
            File.ReadLines(patchPath),
            attempt.WorkspacePath);
    }

    internal static Dictionary<string, HashSet<int>> ParseAddedLines(
        IEnumerable<string> patchLines,
        string workspacePath)
    {
        var result = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        string? currentPath = null;
        var newLine = 0;
        var inHunk = false;

        foreach (var line in patchLines)
        {
            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                currentPath = ResolvePatchPath(workspacePath, line[4..]);
                inHunk = false;
                continue;
            }

            if (line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                inHunk = currentPath != null && TryReadNewHunkStart(line, out newLine);
                continue;
            }

            if (!inHunk || currentPath == null || line.Length == 0)
                continue;

            switch (line[0])
            {
                case '+':
                    if (!result.TryGetValue(currentPath, out var addedLines))
                    {
                        addedLines = [];
                        result[currentPath] = addedLines;
                    }

                    addedLines.Add(newLine - 1);
                    newLine++;
                    break;
                case '-':
                case '\\':
                    break;
                default:
                    newLine++;
                    break;
            }
        }

        return result;
    }

    private static string? ResolvePatchPath(string workspacePath, string rawPath)
    {
        var path = rawPath.Trim();
        if (path == "/dev/null")
            return null;
        if (path.StartsWith("b/", StringComparison.Ordinal))
            path = path[2..];

        path = path.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(workspacePath, path));
    }

    private static bool TryReadNewHunkStart(string header, out int startLine)
    {
        startLine = 0;
        var plus = header.IndexOf('+');
        if (plus < 0)
            return false;

        var end = header.IndexOfAny([',', ' '], plus + 1);
        if (end < 0)
            return false;

        return int.TryParse(header[(plus + 1)..end], out startLine);
    }
}
