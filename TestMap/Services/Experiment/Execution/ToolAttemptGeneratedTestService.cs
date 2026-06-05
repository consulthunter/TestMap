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

        // Build the set of absolute host paths for files changed by the tool.
        var absolutePaths = BuildAbsolutePaths(attempt.WorkspacePath, changedFiles);
        if (absolutePaths.Count == 0)
            return new ToolAttemptGeneratedTestLinkResult();

        // Find test members whose containing file is one of the changed files.
        // Join path: MemberEntity → ObjectEntity → FileEntity
        var memberIds = await (
            from member in _dbContext.Members
            join obj in _dbContext.Objects on member.ObjectEntityId equals obj.Id
            join file in _dbContext.Files on obj.FileId equals file.Id
            where member.IsTestMember
                  && member.Kind == "method"
                  && absolutePaths.Contains(file.FilePath)
            select member.Id
        ).ToListAsync(cancellationToken);

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
}
