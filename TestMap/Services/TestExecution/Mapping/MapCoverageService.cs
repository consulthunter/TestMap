using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Mappings;
using TestMap.Persistence.Ef.Repositories.Code;
using TestMap.Persistence.Ef.Repositories.Coverage;

namespace TestMap.Services.TestExecution.Mapping;

public class MapCoverageService(
    ProjectContext context,
    TestMapDbContext dbContext,
    CoverageReportRepository coverageReportRepository,
    ObjectRepository objectRepository,
    MemberRepository memberRepository,
    FileRepository fileRepository)
{
    public async Task MapAsync(CoverageReportModel report)
    {
        if (context.Project.DbId == 0)
        {
            context.Project.Logger?.Warning("Skipping coverage mapping because the project database ID is not set.");
            return;
        }

        var existingReport = await dbContext.CoverageReports.FirstOrDefaultAsync(x =>
            x.ProjectId == context.Project.DbId && x.Timestamp == report.Timestamp);
        var reportId = await coverageReportRepository.InsertOrUpdateAsync(report, context.Project.DbId, existingReport);
        var objects = await objectRepository.GetAllAsync();
        var members = await memberRepository.GetAllAsync();
        var files = await fileRepository.GetAllAsync();
        var existingObjectCoverages = await dbContext.ObjectCoverages
            .Where(x => x.CoverageReportId == reportId)
            .ToListAsync();
        var existingMemberCoverages = await dbContext.MemberCoverages
            .Where(x => x.CoverageReportId == reportId)
            .ToListAsync();
        var existingCoverageGaps = await dbContext.CoverageGaps
            .Where(x => x.CoverageReportId == reportId)
            .ToListAsync();
        var membersByObjectId = members
            .Where(x => x.ObjectEntityId > 0)
            .GroupBy(x => x.ObjectEntityId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var membersByObjectIdAndName = membersByObjectId.ToDictionary(
            x => x.Key,
            x => x.Value
                .GroupBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase));
        var filePathById = files
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.FilePath))
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().FilePath);
        var normalizedProjectFiles = filePathById.Values
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var objectsByQualifiedName = objects
            .Where(x => x.Id > 0)
            .GroupBy(BuildQualifiedObjectName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var objectsBySimpleName = objects
            .Where(x => x.Id > 0)
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var objectsByFileName = objects
            .Where(x => x.FileId > 0 && filePathById.ContainsKey(x.FileId))
            .GroupBy(x => NormalizePath(Path.GetFileName(filePathById[x.FileId])), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var objectCoverageByKey = existingObjectCoverages.ToDictionary(x => (x.ObjectId, x.CoverageReportId));
        var memberCoverageByKey = existingMemberCoverages.ToDictionary(x => (x.MemberId, x.CoverageReportId));
        var gapsByMember = existingCoverageGaps
            .GroupBy(x => x.MemberId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var fileLinesCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var package in report.Packages)
        foreach (var objectCoverage in package.Classes)
        {
            var normalizedCoverageName = NormalizeTypeName(objectCoverage.Name);
            var normalizedCoverageFilename = NormalizePath(objectCoverage.Filename);
            if (!IsProjectCoverageObject(normalizedCoverageFilename, normalizedProjectFiles)) continue;

            var objectCandidates = GetObjectCandidates(
                normalizedCoverageName,
                normalizedCoverageFilename,
                objectsByQualifiedName,
                objectsBySimpleName,
                objectsByFileName);

            var objectModel = SelectBestObjectMatch(
                objectCandidates,
                objectCoverage,
                membersByObjectId,
                filePathById,
                normalizedCoverageName,
                normalizedCoverageFilename);

            if (objectModel == null)
            {
                context.Project.Logger?.Warning(
                    $"Coverage object '{objectCoverage.Name}' not found in persisted code objects.");
                continue;
            }

            UpsertObjectCoverage(
                dbContext,
                objectCoverage,
                objectModel.Id,
                reportId,
                objectCoverageByKey);
            var objectMembers = membersByObjectId.GetValueOrDefault(objectModel.Id) ?? [];
            var objectMembersByName = membersByObjectIdAndName.GetValueOrDefault(objectModel.Id);

            foreach (var memberCoverage in objectCoverage.Methods)
            {
                var normalizedMemberName = NormalizeMemberName(memberCoverage.Name);
                if (string.IsNullOrWhiteSpace(normalizedMemberName)) continue;

                var memberModel = FindMember(
                    normalizedMemberName,
                    memberCoverage.Name,
                    objectMembers,
                    objectMembersByName);

                if (memberModel == null)
                {
                    context.Project.Logger?.Warning(
                        $"Coverage member '{memberCoverage.Name}' not found in persisted members.");
                    continue;
                }

                UpsertMemberCoverage(
                    dbContext,
                    memberCoverage,
                    memberModel.Id,
                    reportId,
                    memberCoverageByKey);
                var coverageGaps = BuildCoverageGaps(
                    memberCoverage,
                    memberModel,
                    objectModel.FileId,
                    reportId,
                    filePathById,
                    fileLinesCache);
                ReplaceCoverageGaps(
                    dbContext,
                    memberModel.Id,
                    coverageGaps,
                    gapsByMember);
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private static List<CoverageGapModel> BuildCoverageGaps(
        MemberCoverageModel memberCoverage,
        Models.Code.MemberModel memberModel,
        int fileId,
        int coverageReportId,
        IReadOnlyDictionary<int, string> filePathById,
        IDictionary<string, string[]> fileLinesCache)
    {
        var gaps = new List<CoverageGapModel>();
        var memberContentHash = memberModel.ContentHash;
        filePathById.TryGetValue(fileId, out var filePath);

        foreach (var line in memberCoverage.Lines)
        {
            var isPartialBranch = IsPartialBranch(line);
            if (line.Hits > 0 && !isPartialBranch) continue;

            gaps.Add(new CoverageGapModel
            {
                MemberId = memberModel.Id,
                CoverageReportId = coverageReportId,
                LineNumber = line.Number,
                Hits = line.Hits,
                IsBranch = IsBranchLine(line),
                ConditionCoverage = line.ConditionCoverage,
                GapKind = isPartialBranch
                    ? CoverageGapKind.PartialBranch
                    : CoverageGapKind.UncoveredLine,
                SourceText = ResolveSourceText(filePath, line.Number, fileLinesCache),
                MemberContentHash = memberContentHash
            });
        }

        return gaps
            .GroupBy(x => new { x.MemberId, x.CoverageReportId, x.LineNumber, x.GapKind })
            .Select(x => x.First())
            .OrderBy(x => x.LineNumber)
            .ThenBy(x => x.GapKind)
            .ToList();
    }

    private static bool IsBranchLine(LineCoverageModel line)
    {
        return line.Branch.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPartialBranch(LineCoverageModel line)
    {
        return IsBranchLine(line) &&
               !string.IsNullOrWhiteSpace(line.ConditionCoverage) &&
               !line.ConditionCoverage.Contains("100%", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveSourceText(
        string? filePath,
        int lineNumber,
        IDictionary<string, string[]> fileLinesCache)
    {
        if (string.IsNullOrWhiteSpace(filePath) || lineNumber <= 0 || !File.Exists(filePath)) return string.Empty;

        if (!fileLinesCache.TryGetValue(filePath, out var lines))
        {
            lines = File.ReadAllLines(filePath);
            fileLinesCache[filePath] = lines;
        }

        return lineNumber <= lines.Length
            ? lines[lineNumber - 1].Trim()
            : string.Empty;
    }

    private static string BuildQualifiedObjectName(Models.Code.ObjectModel model)
    {
        return string.IsNullOrWhiteSpace(model.Namespace)
            ? model.Name
            : $"{model.Namespace}.{model.Name}";
    }

    private static bool TryMatchByFilePath(
        Models.Code.ObjectModel model,
        string normalizedCoverageFilename,
        IReadOnlyDictionary<int, string> filePathById)
    {
        if (string.IsNullOrWhiteSpace(normalizedCoverageFilename) ||
            model.FileId == 0 ||
            !filePathById.TryGetValue(model.FileId, out var filePath) ||
            string.IsNullOrWhiteSpace(filePath))
            return false;

        var normalizedFilePath = NormalizePath(filePath);
        return normalizedFilePath.EndsWith(normalizedCoverageFilename, StringComparison.OrdinalIgnoreCase) ||
               normalizedCoverageFilename.EndsWith(Path.GetFileName(normalizedFilePath),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjectCoverageObject(
        string normalizedCoverageFilename,
        IReadOnlySet<string> normalizedProjectFiles)
    {
        if (string.IsNullOrWhiteSpace(normalizedCoverageFilename)) return false;

        return normalizedProjectFiles.Any(projectFile =>
            projectFile.EndsWith(normalizedCoverageFilename, StringComparison.OrdinalIgnoreCase) ||
            normalizedCoverageFilename.EndsWith(Path.GetFileName(projectFile), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeTypeName(string value)
    {
        return value.Replace('/', '.').Replace('+', '.').Trim();
    }

    private static string NormalizeMemberName(string value)
    {
        var trimmedValue = value.Trim();
        if (trimmedValue.StartsWith("<", StringComparison.Ordinal)) return string.Empty;

        if (trimmedValue.Equals(".ctor", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.Equals(".cctor", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (trimmedValue.StartsWith("get_", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.StartsWith("set_", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.StartsWith("init_", StringComparison.OrdinalIgnoreCase))
        {
            var underscoreIndex = trimmedValue.IndexOf('_');
            var accessorName = underscoreIndex >= 0 && underscoreIndex < trimmedValue.Length - 1
                ? trimmedValue[(underscoreIndex + 1)..]
                : trimmedValue;
            return StripMemberDecorations(accessorName);
        }

        return StripMemberDecorations(trimmedValue);
    }

    private static string NormalizePath(string value)
    {
        return value.Replace('\\', '/').Trim();
    }

    private static bool IsCoverageCompatible(Models.Code.MemberModel member, string coverageMemberName)
    {
        var trimmedValue = coverageMemberName.Trim();
        if (trimmedValue.StartsWith("get_", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.StartsWith("set_", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.StartsWith("init_", StringComparison.OrdinalIgnoreCase))
            return member.Kind.Equals("property", StringComparison.OrdinalIgnoreCase) ||
                   member.Kind.Equals("indexer", StringComparison.OrdinalIgnoreCase);

        return !member.Kind.Equals("property", StringComparison.OrdinalIgnoreCase) &&
               !member.Kind.Equals("indexer", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripMemberDecorations(string value)
    {
        var normalized = value.Trim();
        var parameterIndex = normalized.IndexOf('(');
        if (parameterIndex >= 0) normalized = normalized[..parameterIndex];

        var genericIndex = normalized.IndexOf('<');
        if (genericIndex >= 0) normalized = normalized[..genericIndex];

        return normalized.Trim();
    }

    private static Models.Code.ObjectModel? SelectBestObjectMatch(
        IReadOnlyCollection<Models.Code.ObjectModel> candidates,
        ObjectCoverageModel objectCoverage,
        IReadOnlyDictionary<int, List<Models.Code.MemberModel>> membersByObjectId,
        IReadOnlyDictionary<int, string> filePathById,
        string normalizedCoverageName,
        string normalizedCoverageFilename)
    {
        if (candidates.Count == 0) return null;

        var normalizedCoverageMembers = objectCoverage.Methods
            .Select(x => x.Name)
            .Select(NormalizeMemberName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = ScoreObjectCandidate(
                    candidate,
                    normalizedCoverageName,
                    normalizedCoverageFilename,
                    normalizedCoverageMembers,
                    membersByObjectId,
                    filePathById)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Candidate.Id)
            .Select(x => x.Candidate)
            .FirstOrDefault();
    }

    private static int ScoreObjectCandidate(
        Models.Code.ObjectModel candidate,
        string normalizedCoverageName,
        string normalizedCoverageFilename,
        IReadOnlySet<string> normalizedCoverageMembers,
        IReadOnlyDictionary<int, List<Models.Code.MemberModel>> membersByObjectId,
        IReadOnlyDictionary<int, string> filePathById)
    {
        var score = 0;

        if (BuildQualifiedObjectName(candidate)
            .Equals(normalizedCoverageName, StringComparison.OrdinalIgnoreCase)) score += 100;

        if (candidate.Name.Equals(normalizedCoverageName, StringComparison.OrdinalIgnoreCase)) score += 60;

        if (TryMatchByFilePath(candidate, normalizedCoverageFilename, filePathById)) score += 40;

        if (membersByObjectId.TryGetValue(candidate.Id, out var objectMembers))
            score += objectMembers.Count(member =>
                normalizedCoverageMembers.Contains(member.Name) &&
                IsCoverageCompatible(member, member.Name)) * 10;

        return score;
    }

    private static IReadOnlyCollection<Models.Code.ObjectModel> GetObjectCandidates(
        string normalizedCoverageName,
        string normalizedCoverageFilename,
        IReadOnlyDictionary<string, List<Models.Code.ObjectModel>> objectsByQualifiedName,
        IReadOnlyDictionary<string, List<Models.Code.ObjectModel>> objectsBySimpleName,
        IReadOnlyDictionary<string, List<Models.Code.ObjectModel>> objectsByFileName)
    {
        var candidates = new Dictionary<int, Models.Code.ObjectModel>();

        AddCandidates(candidates, objectsByQualifiedName, normalizedCoverageName);
        AddCandidates(candidates, objectsBySimpleName, normalizedCoverageName);

        if (!string.IsNullOrWhiteSpace(normalizedCoverageFilename))
            AddCandidates(candidates, objectsByFileName, NormalizePath(Path.GetFileName(normalizedCoverageFilename)));

        return candidates.Values.ToList();
    }

    private static void AddCandidates(
        IDictionary<int, Models.Code.ObjectModel> candidates,
        IReadOnlyDictionary<string, List<Models.Code.ObjectModel>> source,
        string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !source.TryGetValue(key, out var matches)) return;

        foreach (var match in matches) candidates[match.Id] = match;
    }

    private static Models.Code.MemberModel? FindMember(
        string normalizedMemberName,
        string coverageMemberName,
        IReadOnlyCollection<Models.Code.MemberModel> objectMembers,
        IReadOnlyDictionary<string, List<Models.Code.MemberModel>>? objectMembersByName)
    {
        if (objectMembersByName != null &&
            objectMembersByName.TryGetValue(normalizedMemberName, out var namedMatches))
            return namedMatches.FirstOrDefault(x => IsCoverageCompatible(x, coverageMemberName));

        return objectMembers.FirstOrDefault(x =>
            x.Name.Equals(normalizedMemberName, StringComparison.OrdinalIgnoreCase) &&
            IsCoverageCompatible(x, coverageMemberName));
    }

    private static void UpsertObjectCoverage(
        TestMapDbContext dbContext,
        ObjectCoverageModel model,
        int objectId,
        int reportId,
        IDictionary<(int ObjectId, int CoverageReportId), ObjectCoverageEntity> cache)
    {
        var key = (objectId, reportId);
        if (cache.TryGetValue(key, out var existing))
        {
            if (ObjectCoverageRepository.HasChanged(existing, model)) ObjectCoverageRepository.Apply(existing, model);

            return;
        }

        var entity = model.ToEntity(objectId, reportId);
        dbContext.ObjectCoverages.Add(entity);
        cache[key] = entity;
    }

    private static void UpsertMemberCoverage(
        TestMapDbContext dbContext,
        MemberCoverageModel model,
        int memberId,
        int reportId,
        IDictionary<(int MemberId, int CoverageReportId), MemberCoverageEntity> cache)
    {
        var key = (memberId, reportId);
        if (cache.TryGetValue(key, out var existing))
        {
            if (MemberCoverageRepository.HasChanged(existing, model)) MemberCoverageRepository.Apply(existing, model);

            return;
        }

        var entity = model.ToEntity(memberId, reportId);
        dbContext.MemberCoverages.Add(entity);
        cache[key] = entity;
    }

    private static void ReplaceCoverageGaps(
        TestMapDbContext dbContext,
        int memberId,
        IReadOnlyCollection<CoverageGapModel> gaps,
        IDictionary<int, List<CoverageGapEntity>> gapsByMember)
    {
        if (gapsByMember.TryGetValue(memberId, out var existing) && existing.Count > 0)
            dbContext.CoverageGaps.RemoveRange(existing);

        var newEntities = gaps.Select(x => x.ToEntity()).ToList();
        if (newEntities.Count > 0) dbContext.CoverageGaps.AddRange(newEntities);

        gapsByMember[memberId] = newEntities;
    }
}