using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Models.Results;
using TestMap.Persistence.Ef.Entities.MutationTesting;
using TestMap.Persistence.Ef.Mappings;

namespace TestMap.Persistence.Ef.Repositories.MutationTesting;

public class MutationTestingReportRepository
{
    private readonly TestMapDbContext _context;

    public MutationTestingReportRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<StrykerMutationResults>> GetAllAsync()
    {
        var entities = await _context.MutationTestingReports.ToListAsync();
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<StrykerMutationResults?> GetByIdAsync(int id)
    {
        var entity = await _context.MutationTestingReports.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<int> InsertOrUpdateAsync(StrykerMutationResults model, int projectId, double mutationScore)
    {
        var existing = await _context.MutationTestingReports.FirstOrDefaultAsync(x =>
            x.ProjectId == projectId &&
            x.SchemaVersion == model.schemaVersion &&
            x.ProjectRoot == model.projectRoot);

        if (existing != null)
        {
            if (HasChanged(existing, model, mutationScore))
            {
                existing.MutationScore = mutationScore;
                existing.Files = model.files;
                existing.TestFiles = model.testFiles;
                existing.Thresholds = model.thresholds;
                await _context.SaveChangesAsync();
            }

            await PersistMutantsAsync(existing.Id, model);
            return existing.Id;
        }

        var entity = model.ToEntity(projectId, mutationScore);
        _context.MutationTestingReports.Add(entity);
        await _context.SaveChangesAsync();
        await PersistMutantsAsync(entity.Id, model);
        return entity.Id;
    }

    public async Task<bool> HasReportsAsync(int projectId)
    {
        return await _context.MutationTestingReports.AnyAsync(x => x.ProjectId == projectId);
    }

    private static bool HasChanged(
        MutationTestingReportEntity entity,
        StrykerMutationResults model,
        double mutationScore)
    {
        return entity.MutationScore != mutationScore ||
               entity.SchemaVersion != model.schemaVersion ||
               entity.ProjectRoot != model.projectRoot;
    }

    private async Task PersistMutantsAsync(int reportId, StrykerMutationResults model)
    {
        var testsById = BuildTestsById(model);
        var existingMutants = await _context.Mutants
            .Where(x => x.MutationTestingReportId == reportId)
            .ToListAsync();
        var memberCandidates = await _context.Members
            .Join(
                _context.Objects,
                member => member.ObjectEntityId,
                obj => obj.Id,
                (member, obj) => new { Member = member, Object = obj })
            .Join(
                _context.Files,
                memberObject => memberObject.Object.FileId,
                file => file.Id,
                (memberObject, file) => new MemberCandidate(memberObject.Member, file))
            .ToListAsync();
        var memberCandidatesByTestFlag = memberCandidates
            .GroupBy(x => x.Member.IsTestMember)
            .ToDictionary(x => x.Key, x => x.ToList());
        var resolvedMemberIds = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        var mutantsByHash = existingMutants.ToDictionary(x => x.ContentHash);
        var pendingSurvivedTests = new List<(MutantEntity Mutant, string TestId, TestContext? TestContext)>();

        foreach (var (filePath, fileResult) in model.files ?? new Dictionary<string, StrykerFileResult>())
        foreach (var mutant in fileResult.mutants ?? new List<StrykerMutant>())
        {
            var mutantEntity = UpsertMutant(
                reportId,
                model.projectRoot,
                filePath,
                mutant,
                memberCandidatesByTestFlag,
                resolvedMemberIds,
                mutantsByHash);

            if (!string.Equals(mutant.status, "Survived", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var testId in GetSurvivedTestIds(mutant))
            {
                testsById.TryGetValue(testId, out var testContext);
                pendingSurvivedTests.Add((mutantEntity, testId, testContext));
            }
        }

        await _context.SaveChangesAsync();

        var mutantIds = existingMutants
            .Select(x => x.Id)
            .Concat(pendingSurvivedTests.Select(x => x.Mutant.Id))
            .Distinct()
            .ToList();
        var existingSurvivedTests = await _context.MutantSurvivedTests
            .Where(x => mutantIds.Contains(x.MutantId))
            .ToListAsync();
        var survivedTestsByHash = existingSurvivedTests.ToDictionary(x => x.ContentHash);

        foreach (var pending in pendingSurvivedTests)
            UpsertSurvivedTest(
                pending.Mutant,
                model.projectRoot,
                pending.TestId,
                pending.TestContext,
                memberCandidatesByTestFlag,
                resolvedMemberIds,
                survivedTestsByHash);

        await _context.SaveChangesAsync();
    }

    private MutantEntity UpsertMutant(
        int reportId,
        string projectRoot,
        string filePath,
        StrykerMutant mutant,
        IReadOnlyDictionary<bool, List<MemberCandidate>> memberCandidatesByTestFlag,
        IDictionary<string, int?> resolvedMemberIds,
        IDictionary<string, MutantEntity> mutantsByHash)
    {
        var normalizedFilePath = NormalizePath(filePath, projectRoot);
        var location = ToLocation(mutant.location);
        var contentHash = Utilities.Utilities.ComputeSha256(
            $"{reportId}:{normalizedFilePath}:{mutant.id}:{location.StartLineNumber}:{location.BodyStartPosition}:{mutant.mutatorName}:{mutant.replacement}");

        var memberId = ResolveMemberId(
            filePath,
            projectRoot,
            location,
            false,
            memberCandidatesByTestFlag,
            resolvedMemberIds);

        if (mutantsByHash.TryGetValue(contentHash, out var existing))
        {
            existing.MemberId = memberId;
            existing.Status = mutant.status ?? string.Empty;
            existing.StatusReason = mutant.statusReason ?? string.Empty;
            existing.CoveredBy = mutant.coveredBy ?? new List<string>();
            existing.KilledBy = mutant.killedBy ?? new List<string>();
            return existing;
        }

        var entity = new MutantEntity
        {
            MutationTestingReportId = reportId,
            MemberId = memberId,
            StrykerMutantId = mutant.id ?? string.Empty,
            FilePath = filePath,
            MutatorName = mutant.mutatorName ?? string.Empty,
            Replacement = mutant.replacement ?? string.Empty,
            Status = mutant.status ?? string.Empty,
            StatusReason = mutant.statusReason ?? string.Empty,
            IsStatic = mutant.@static,
            Location = location,
            CoveredBy = mutant.coveredBy ?? new List<string>(),
            KilledBy = mutant.killedBy ?? new List<string>(),
            ContentHash = contentHash,
            CreatedAt = DateTime.UtcNow
        };

        _context.Mutants.Add(entity);
        mutantsByHash[contentHash] = entity;
        return entity;
    }

    private void UpsertSurvivedTest(
        MutantEntity mutantEntity,
        string projectRoot,
        string testId,
        TestContext? testContext,
        IReadOnlyDictionary<bool, List<MemberCandidate>> memberCandidatesByTestFlag,
        IDictionary<string, int?> resolvedMemberIds,
        IDictionary<string, MutantSurvivedTestEntity> survivedTestsByHash)
    {
        var contentHash = Utilities.Utilities.ComputeSha256($"{mutantEntity.Id}:{testId}");
        var testFilePath = testContext?.FilePath ?? string.Empty;
        var location = ToLocation(testContext?.Test.location);
        var testMemberId = string.IsNullOrWhiteSpace(testFilePath)
            ? null
            : ResolveMemberId(
                testFilePath,
                projectRoot,
                location,
                true,
                memberCandidatesByTestFlag,
                resolvedMemberIds,
                testContext?.Test.name);

        if (survivedTestsByHash.TryGetValue(contentHash, out var existing))
        {
            existing.TestMemberId = testMemberId;
            existing.TestName = testContext?.Test.name ?? string.Empty;
            existing.TestFilePath = testFilePath;
            existing.Location = location;
            return;
        }

        var entity = new MutantSurvivedTestEntity
        {
            MutantId = mutantEntity.Id,
            TestMemberId = testMemberId,
            StrykerTestId = testId,
            TestName = testContext?.Test.name ?? string.Empty,
            TestFilePath = testFilePath,
            Location = location,
            ContentHash = contentHash
        };

        _context.MutantSurvivedTests.Add(entity);
        survivedTestsByHash[contentHash] = entity;
    }

    private static int? ResolveMemberId(
        string reportedFilePath,
        string projectRoot,
        Location location,
        bool isTestMember,
        IReadOnlyDictionary<bool, List<MemberCandidate>> memberCandidatesByTestFlag,
        IDictionary<string, int?> resolvedMemberIds,
        string? memberName = null)
    {
        var cacheKey = string.Join(
            "|",
            isTestMember,
            NormalizeForComparison(reportedFilePath),
            NormalizeForComparison(projectRoot),
            location.StartLineNumber,
            location.EndLineNumber,
            memberName ?? string.Empty);
        if (resolvedMemberIds.TryGetValue(cacheKey, out var cached)) return cached;

        var line = location.StartLineNumber;
        memberCandidatesByTestFlag.TryGetValue(isTestMember, out var candidates);
        candidates ??= [];

        var fileMatches = candidates
            .Where(x => PathsMatch(x.File.FilePath, reportedFilePath, projectRoot))
            .ToList();

        var lineMatch = fileMatches
            .Where(x => x.Member.Location.StartLineNumber <= line && line <= x.Member.Location.EndLineNumber)
            .OrderBy(x => Math.Max(0, x.Member.Location.EndLineNumber - x.Member.Location.StartLineNumber))
            .Select(x => (int?)x.Member.Id)
            .FirstOrDefault();

        if (lineMatch != null)
        {
            resolvedMemberIds[cacheKey] = lineMatch;
            return lineMatch;
        }

        if (!string.IsNullOrWhiteSpace(memberName))
        {
            var simpleName = memberName.Split('.').Last();
            var match = fileMatches
                .Where(x =>
                    string.Equals(x.Member.Name, simpleName, StringComparison.OrdinalIgnoreCase) ||
                    memberName.EndsWith("." + x.Member.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Member.IsTestMember == isTestMember ? 0 : 1)
                .Select(x => (int?)x.Member.Id)
                .FirstOrDefault();
            resolvedMemberIds[cacheKey] = match;
            return match;
        }

        resolvedMemberIds[cacheKey] = null;
        return null;
    }

    private static Dictionary<string, TestContext> BuildTestsById(StrykerMutationResults model)
    {
        var testsById = new Dictionary<string, TestContext>(StringComparer.Ordinal);
        foreach (var (filePath, testFile) in model.testFiles ?? new Dictionary<string, StrykerTestFileResult>())
        foreach (var test in testFile.tests ?? new List<StrykerTest>())
            if (!string.IsNullOrWhiteSpace(test.id))
                testsById[test.id] = new TestContext(filePath, test);

        return testsById;
    }

    private static IEnumerable<string> GetSurvivedTestIds(StrykerMutant mutant)
    {
        var killedBy = new HashSet<string>(mutant.killedBy ?? new List<string>(), StringComparer.Ordinal);
        return (mutant.coveredBy ?? new List<string>())
            .Where(testId => !killedBy.Contains(testId))
            .Distinct(StringComparer.Ordinal);
    }

    private static Location ToLocation(StrykerLocation? location)
    {
        return new Location(
            Math.Max(0, (location?.start?.line ?? 1) - 1),
            Math.Max(0, (location?.start?.column ?? 1) - 1),
            Math.Max(0, (location?.end?.line ?? 1) - 1),
            Math.Max(0, (location?.end?.column ?? 1) - 1));
    }

    private static string NormalizePath(string path, string projectRoot)
    {
        var fullPath = Path.IsPathFullyQualified(path)
            ? path
            : string.IsNullOrWhiteSpace(projectRoot)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(projectRoot, path));

        return fullPath
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
    }

    private static bool PathsMatch(string storedPath, string reportedPath, string projectRoot)
    {
        var stored = NormalizeForComparison(storedPath);
        var reported = NormalizeForComparison(reportedPath);
        var reportedRelative = ToProjectRelativeComparisonPath(reported, projectRoot);

        return stored == reported ||
               stored == reportedRelative ||
               stored.EndsWith("/" + reportedRelative, StringComparison.OrdinalIgnoreCase) ||
               reported.EndsWith("/" + stored, StringComparison.OrdinalIgnoreCase);
    }

    private static string ToProjectRelativeComparisonPath(string normalizedPath, string projectRoot)
    {
        var normalizedProjectRoot = NormalizeForComparison(projectRoot);
        if (!string.IsNullOrWhiteSpace(normalizedProjectRoot) &&
            normalizedPath.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
            return normalizedPath[(normalizedProjectRoot.Length + 1)..];

        const string containerProjectRoot = "/APP/PROJECT/";
        var containerRootIndex = normalizedPath.IndexOf(containerProjectRoot, StringComparison.OrdinalIgnoreCase);
        if (containerRootIndex >= 0) return normalizedPath[(containerRootIndex + containerProjectRoot.Length)..];

        return normalizedPath;
    }

    private static string NormalizeForComparison(string path)
    {
        return (path ?? string.Empty)
            .Replace('\\', '/')
            .Trim()
            .TrimEnd('/')
            .ToUpperInvariant();
    }

    private sealed record MemberCandidate(
        Persistence.Ef.Entities.Code.MemberEntity Member,
        Persistence.Ef.Entities.Code.FileEntity File);

    private sealed record TestContext(string FilePath, StrykerTest Test);
}