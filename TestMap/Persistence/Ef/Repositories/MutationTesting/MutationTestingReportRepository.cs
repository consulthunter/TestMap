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
        Entities.MutationTesting.MutationTestingReportEntity entity,
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

        foreach (var (filePath, fileResult) in model.files ?? new Dictionary<string, StrykerFileResult>())
        {
            foreach (var mutant in fileResult.mutants ?? new List<StrykerMutant>())
            {
                var mutantEntity = await UpsertMutantAsync(reportId, model.projectRoot, filePath, mutant);

                if (!string.Equals(mutant.status, "Survived", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var testId in GetSurvivedTestIds(mutant))
                {
                    testsById.TryGetValue(testId, out var testContext);
                    await UpsertSurvivedTestAsync(mutantEntity.Id, model.projectRoot, testId, testContext);
                }
            }
        }
    }

    private async Task<MutantEntity> UpsertMutantAsync(int reportId, string projectRoot, string filePath, StrykerMutant mutant)
    {
        var normalizedFilePath = NormalizePath(filePath, projectRoot);
        var location = ToLocation(mutant.location);
        var contentHash = Utilities.Utilities.ComputeSha256(
            $"{reportId}:{normalizedFilePath}:{mutant.id}:{location.StartLineNumber}:{location.BodyStartPosition}:{mutant.mutatorName}:{mutant.replacement}");

        var existing = await _context.Mutants.FirstOrDefaultAsync(x => x.ContentHash == contentHash);
        if (existing != null)
        {
            existing.MemberId = await ResolveMemberIdAsync(filePath, projectRoot, location, isTestMember: false);
            existing.Status = mutant.status ?? string.Empty;
            existing.StatusReason = mutant.statusReason ?? string.Empty;
            existing.CoveredBy = mutant.coveredBy ?? new List<string>();
            existing.KilledBy = mutant.killedBy ?? new List<string>();
            await _context.SaveChangesAsync();
            return existing;
        }

        var entity = new MutantEntity
        {
            MutationTestingReportId = reportId,
            MemberId = await ResolveMemberIdAsync(filePath, projectRoot, location, isTestMember: false),
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
        await _context.SaveChangesAsync();
        return entity;
    }

    private async Task UpsertSurvivedTestAsync(int mutantId, string projectRoot, string testId, TestContext? testContext)
    {
        var contentHash = Utilities.Utilities.ComputeSha256($"{mutantId}:{testId}");
        var testFilePath = testContext?.FilePath ?? string.Empty;
        var location = ToLocation(testContext?.Test.location);
        var testMemberId = string.IsNullOrWhiteSpace(testFilePath)
            ? null
            : await ResolveMemberIdAsync(
                testFilePath,
                projectRoot,
                location,
                isTestMember: true,
                testContext?.Test.name);

        var existing = await _context.MutantSurvivedTests.FirstOrDefaultAsync(x => x.ContentHash == contentHash);
        if (existing != null)
        {
            existing.TestMemberId = testMemberId;
            existing.TestName = testContext?.Test.name ?? string.Empty;
            existing.TestFilePath = testFilePath;
            existing.Location = location;
            await _context.SaveChangesAsync();
            return;
        }

        var entity = new MutantSurvivedTestEntity
        {
            MutantId = mutantId,
            TestMemberId = testMemberId,
            StrykerTestId = testId,
            TestName = testContext?.Test.name ?? string.Empty,
            TestFilePath = testFilePath,
            Location = location,
            ContentHash = contentHash
        };

        _context.MutantSurvivedTests.Add(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<int?> ResolveMemberIdAsync(
        string reportedFilePath,
        string projectRoot,
        Location location,
        bool isTestMember,
        string? memberName = null)
    {
        var line = location.StartLineNumber;
        var candidates = await _context.Members
            .Join(
                _context.Objects,
                member => member.ObjectEntityId,
                obj => obj.Id,
                (member, obj) => new { Member = member, Object = obj })
            .Join(
                _context.Files,
                memberObject => memberObject.Object.FileId,
                file => file.Id,
                (memberObject, file) => new { memberObject.Member, File = file })
            .Where(x => x.Member.IsTestMember == isTestMember)
            .ToListAsync();

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
            return lineMatch;
        }

        if (!string.IsNullOrWhiteSpace(memberName))
        {
            var simpleName = memberName.Split('.').Last();
            return fileMatches
                .Where(x =>
                    string.Equals(x.Member.Name, simpleName, StringComparison.OrdinalIgnoreCase) ||
                    memberName.EndsWith("." + x.Member.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Member.IsTestMember == isTestMember ? 0 : 1)
                .Select(x => (int?)x.Member.Id)
                .FirstOrDefault();
        }

        return null;
    }

    private static Dictionary<string, TestContext> BuildTestsById(StrykerMutationResults model)
    {
        var testsById = new Dictionary<string, TestContext>(StringComparer.Ordinal);
        foreach (var (filePath, testFile) in model.testFiles ?? new Dictionary<string, StrykerTestFileResult>())
        {
            foreach (var test in testFile.tests ?? new List<StrykerTest>())
            {
                if (!string.IsNullOrWhiteSpace(test.id))
                {
                    testsById[test.id] = new TestContext(filePath, test);
                }
            }
        }

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
        => new(
            Math.Max(0, (location?.start?.line ?? 1) - 1),
            Math.Max(0, (location?.start?.column ?? 1) - 1),
            Math.Max(0, (location?.end?.line ?? 1) - 1),
            Math.Max(0, (location?.end?.column ?? 1) - 1));

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
        {
            return normalizedPath[(normalizedProjectRoot.Length + 1)..];
        }

        const string containerProjectRoot = "/APP/PROJECT/";
        var containerRootIndex = normalizedPath.IndexOf(containerProjectRoot, StringComparison.OrdinalIgnoreCase);
        if (containerRootIndex >= 0)
        {
            return normalizedPath[(containerRootIndex + containerProjectRoot.Length)..];
        }

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

    private sealed record TestContext(string FilePath, StrykerTest Test);
}
