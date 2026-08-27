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

        if ((report.LineCountsAvailable && report.LinesCovered > report.LinesValid) ||
            (report.BranchCountsAvailable && report.BranchesCovered > report.BranchesValid))
        {
            report.CollectionStatus = "ParseFailed";
            report.CollectionReason = "Coverage report contains a covered count greater than its valid count.";
        }

        report.MeasurementPolicyVersion = CoverageReportModel.CorrectedPolicyVersion;
        var collectionFailedBeforeAttribution = IsPreAttributionFailure(report.CollectionStatus);
        if (!collectionFailedBeforeAttribution) report.CollectionStatus = "PendingAttribution";
        var existingReport = await FindReportAsync(report);
        var reportId = await coverageReportRepository.InsertOrUpdateAsync(report, context.Project.DbId, existingReport);
        if (collectionFailedBeforeAttribution) return;
        var objects = await objectRepository.GetAllAsync();
        var members = await memberRepository.GetAllAsync();
        var files = await fileRepository.GetAllAsync();
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
            .GroupBy(x => CoverageTypeName.Normalize(BuildQualifiedObjectName(x)), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var objectsBySimpleName = objects
            .Where(x => x.Id > 0)
            .GroupBy(x => CoverageTypeName.SimpleName(x.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var objectsByFileName = objects
            .Where(x => x.FileId > 0 && filePathById.ContainsKey(x.FileId))
            .GroupBy(x => NormalizePath(Path.GetFileName(filePathById[x.FileId])), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var gapsByMember = existingCoverageGaps
            .GroupBy(x => x.MemberId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var fileLinesCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var rawObjects = await PersistRawObservationsAsync(report, reportId);
        await dbContext.SaveChangesAsync();

        foreach (var rawObject in rawObjects)
        {
            var objectCoverage = rawObject.Model;
            var objectEntity = rawObject.Entity;
            if (!string.IsNullOrWhiteSpace(objectCoverage.CounterValidationError))
            {
                SetObjectOutcome(objectEntity, "Unsupported", objectCoverage.CounterValidationError);
                SetChildOutcomes(rawObject, "ParentUnmatched", "Owning coverage class has invalid counters.");
                continue;
            }

            var normalizedCoverageName = CoverageTypeName.Normalize(objectCoverage.Name);
            var normalizedCoverageFilename = NormalizePath(objectCoverage.Filename);
            if (string.IsNullOrWhiteSpace(normalizedCoverageName) ||
                string.IsNullOrWhiteSpace(normalizedCoverageFilename))
            {
                SetObjectOutcome(objectEntity, "Unsupported", "Coverage class is missing a usable name or filename.");
                SetChildOutcomes(rawObject, "ParentUnmatched", "Owning coverage class is unsupported.");
                continue;
            }

            if (!IsProjectCoverageObject(normalizedCoverageFilename, normalizedProjectFiles))
            {
                SetObjectOutcome(objectEntity, "OutOfProject", "Coverage filename does not match a persisted project file.");
                SetChildOutcomes(rawObject, "ParentUnmatched", "Owning coverage class is outside the persisted project.");
                continue;
            }

            var objectCandidates = GetObjectCandidates(
                normalizedCoverageName,
                normalizedCoverageFilename,
                objectsByQualifiedName,
                objectsBySimpleName,
                objectsByFileName);

            var objectMatch = SelectBestObjectMatch(
                objectCandidates,
                objectCoverage,
                membersByObjectId,
                filePathById,
                normalizedCoverageName,
                normalizedCoverageFilename);

            if (objectMatch.IsAmbiguous)
            {
                SetObjectOutcome(objectEntity, "Ambiguous", "Multiple persisted code objects have the same best attribution score.");
                SetChildOutcomes(rawObject, "ParentUnmatched", "Owning coverage class attribution is ambiguous.");
                continue;
            }

            var objectModel = objectMatch.Match;
            if (objectModel == null)
            {
                SetObjectOutcome(objectEntity, "Unmatched", "No persisted code object matched the coverage class.");
                SetChildOutcomes(rawObject, "ParentUnmatched", "Owning coverage class was not matched.");
                context.Project.Logger?.Warning(
                    $"Coverage object '{objectCoverage.Name}' not found in persisted code objects.");
                continue;
            }

            objectEntity.ObjectId = objectModel.Id;
            SetObjectOutcome(objectEntity, "Mapped", string.Empty);
            var objectMembers = membersByObjectId.GetValueOrDefault(objectModel.Id) ?? [];
            var objectMembersByName = membersByObjectIdAndName.GetValueOrDefault(objectModel.Id);

            foreach (var rawMember in rawObject.Members)
            {
                var memberCoverage = rawMember.Model;
                var memberEntity = rawMember.Entity;
                if (!string.IsNullOrWhiteSpace(memberCoverage.CounterValidationError))
                {
                    SetMemberOutcome(memberEntity, "Unsupported", memberCoverage.CounterValidationError);
                    continue;
                }

                var normalizedMemberName = NormalizeMemberName(memberCoverage.Name);
                if (string.IsNullOrWhiteSpace(normalizedMemberName))
                {
                    SetMemberOutcome(memberEntity, "Unsupported", "Coverage member name is compiler-generated or empty.");
                    continue;
                }

                var memberMatch = FindMember(
                    normalizedMemberName,
                    memberCoverage.Name,
                    memberCoverage,
                    objectMembers,
                    objectMembersByName);

                if (memberMatch.IsAmbiguous)
                {
                    SetMemberOutcome(memberEntity, "Ambiguous", "Multiple persisted members remain after line and signature disambiguation.");
                    continue;
                }

                var memberModel = memberMatch.Match;
                if (memberModel == null)
                {
                    SetMemberOutcome(memberEntity, "Unmatched", "No compatible persisted member matched the coverage observation.");
                    context.Project.Logger?.Warning(
                        $"Coverage member '{memberCoverage.Name}' not found in persisted members.");
                    continue;
                }

                memberEntity.MemberId = memberModel.Id;
                SetMemberOutcome(memberEntity, "Mapped", string.Empty);
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

        report.RawObjectCount = rawObjects.Count;
        report.MappedObjectCount = rawObjects.Count(x => x.Entity.AttributionStatus == "Mapped");
        report.RawMemberCount = rawObjects.Sum(x => x.Members.Count);
        report.MappedMemberCount = rawObjects.Sum(x => x.Members.Count(y => y.Entity.AttributionStatus == "Mapped"));
        report.HasUsableCoverage = report.MappedObjectCount > 0 &&
                                   (report.RawMemberCount == 0 || report.MappedMemberCount > 0);
        report.CollectionStatus = report.RawObjectCount == 0
            ? "ParsedNoData"
            : !report.HasUsableCoverage
                ? "ParsedNoUsableCoverage"
                : report.MappedObjectCount == report.RawObjectCount &&
                  report.MappedMemberCount == report.RawMemberCount
                    ? "Mapped"
                    : "PartiallyMapped";
        var reportEntity = await dbContext.CoverageReports.FindAsync(reportId);
        if (reportEntity != null) ApplyReportOutcome(reportEntity, report);
        await dbContext.SaveChangesAsync();
    }

    public async Task LinkToTestRunAsync(CoverageReportModel report, int testRunId)
    {
        var entity = await FindReportAsync(report);
        if (entity == null || entity.TestRunId == testRunId) return;

        entity.TestRunId = testRunId;
        await dbContext.SaveChangesAsync();
    }

    private Task<CoverageReportEntity?> FindReportAsync(CoverageReportModel report)
    {
        return string.IsNullOrWhiteSpace(report.RunId)
            ? dbContext.CoverageReports.FirstOrDefaultAsync(x =>
                x.ProjectId == context.Project.DbId && x.RunId == string.Empty && x.Timestamp == report.Timestamp)
            : dbContext.CoverageReports.FirstOrDefaultAsync(x =>
                x.ProjectId == context.Project.DbId && x.RunId == report.RunId);
    }

    private static bool IsPreAttributionFailure(string status)
    {
        return status is "ProviderUnavailable" or "CollectionFailed" or "NoArtifact" or "MergeFailed" or "ParseFailed";
    }

    private async Task<List<RawObjectObservation>> PersistRawObservationsAsync(
        CoverageReportModel report,
        int reportId)
    {
        var existingObjects = await dbContext.ObjectCoverages
            .Where(x => x.CoverageReportId == reportId && x.SourceOrdinal >= 0)
            .ToListAsync();
        var existingMembers = await dbContext.MemberCoverages
            .Where(x => x.CoverageReportId == reportId && x.SourceOrdinal >= 0)
            .ToListAsync();
        var objectsByOrdinal = existingObjects.ToDictionary(x => x.SourceOrdinal);
        var membersByParentAndOrdinal = existingMembers
            .Where(x => x.ObjectCoverageId.HasValue)
            .ToDictionary(x => (x.ObjectCoverageId!.Value, x.SourceOrdinal));
        var observations = new List<RawObjectObservation>();
        var objectOrdinal = 0;

        foreach (var package in report.Packages)
        foreach (var objectModel in package.Classes)
        {
            ApplyCounters(objectModel, CoverageCounterCalculator.Calculate(objectModel.Lines));
            objectModel.CoverageReportId = reportId;
            objectModel.SourceOrdinal = objectOrdinal;
            objectModel.PackageName = package.Name;
            objectModel.AttributionStatus = "Pending";
            objectModel.AttributionReason = "Awaiting source attribution.";
            objectModel.ObjectId = null;

            if (!objectsByOrdinal.TryGetValue(objectOrdinal, out var objectEntity))
            {
                objectEntity = objectModel.ToEntity(null, reportId);
                dbContext.ObjectCoverages.Add(objectEntity);
            }
            else
            {
                objectEntity.ObjectId = null;
                ObjectCoverageRepository.Apply(objectEntity, objectModel);
            }

            var rawMembers = new List<RawMemberObservation>();
            for (var memberOrdinal = 0; memberOrdinal < objectModel.Methods.Count; memberOrdinal++)
            {
                var memberModel = objectModel.Methods[memberOrdinal];
                ApplyCounters(memberModel, CoverageCounterCalculator.Calculate(memberModel.Lines));
                memberModel.CoverageReportId = reportId;
                memberModel.SourceOrdinal = memberOrdinal;
                memberModel.AttributionStatus = "Pending";
                memberModel.AttributionReason = "Awaiting source attribution.";
                memberModel.MemberId = null;

                MemberCoverageEntity memberEntity;
                if (objectEntity.Id > 0 &&
                    membersByParentAndOrdinal.TryGetValue((objectEntity.Id, memberOrdinal), out var existingMember))
                {
                    memberEntity = existingMember;
                    memberEntity.MemberId = null;
                    memberModel.ObjectCoverageId = objectEntity.Id;
                    MemberCoverageRepository.Apply(memberEntity, memberModel);
                }
                else
                {
                    memberEntity = memberModel.ToEntity(null, reportId);
                    memberEntity.ObjectCoverage = objectEntity;
                    dbContext.MemberCoverages.Add(memberEntity);
                }

                rawMembers.Add(new RawMemberObservation(memberModel, memberEntity));
            }

            if (objectEntity.Id > 0)
            {
                var staleMembers = existingMembers.Where(x =>
                    x.ObjectCoverageId == objectEntity.Id && x.SourceOrdinal >= objectModel.Methods.Count);
                dbContext.MemberCoverages.RemoveRange(staleMembers);
            }

            observations.Add(new RawObjectObservation(objectModel, objectEntity, rawMembers));
            objectOrdinal++;
        }

        dbContext.ObjectCoverages.RemoveRange(existingObjects.Where(x => x.SourceOrdinal >= objectOrdinal));
        return observations;
    }

    private static void ApplyCounters(ObjectCoverageModel model, CoverageCounterResult result)
    {
        model.LinesCovered = result.LinesCovered;
        model.LinesValid = result.LinesValid;
        model.BranchesCovered = result.BranchesCovered;
        model.BranchesValid = result.BranchesValid;
        model.LineCountsAvailable = result.LineCountsAvailable;
        model.BranchCountsAvailable = result.BranchCountsAvailable;
        model.CounterValidationError = result.IsValid ? string.Empty : result.ValidationError;
    }

    private static void ApplyCounters(MemberCoverageModel model, CoverageCounterResult result)
    {
        model.LinesCovered = result.LinesCovered;
        model.LinesValid = result.LinesValid;
        model.BranchesCovered = result.BranchesCovered;
        model.BranchesValid = result.BranchesValid;
        model.LineCountsAvailable = result.LineCountsAvailable;
        model.BranchCountsAvailable = result.BranchCountsAvailable;
        model.CounterValidationError = result.IsValid ? string.Empty : result.ValidationError;
    }

    private static void SetObjectOutcome(ObjectCoverageEntity entity, string status, string reason)
    {
        entity.AttributionStatus = status;
        entity.AttributionReason = reason;
        if (status != "Mapped") entity.ObjectId = null;
    }

    private static void SetMemberOutcome(MemberCoverageEntity entity, string status, string reason)
    {
        entity.AttributionStatus = status;
        entity.AttributionReason = reason;
        if (status != "Mapped") entity.MemberId = null;
    }

    private static void SetChildOutcomes(RawObjectObservation observation, string status, string reason)
    {
        foreach (var member in observation.Members) SetMemberOutcome(member.Entity, status, reason);
    }

    private static void ApplyReportOutcome(CoverageReportEntity entity, CoverageReportModel report)
    {
        entity.CollectionStatus = report.CollectionStatus;
        entity.HasUsableCoverage = report.HasUsableCoverage;
        entity.MeasurementPolicyVersion = report.MeasurementPolicyVersion;
        entity.RawObjectCount = report.RawObjectCount;
        entity.MappedObjectCount = report.MappedObjectCount;
        entity.RawMemberCount = report.RawMemberCount;
        entity.MappedMemberCount = report.MappedMemberCount;
    }

    private sealed record RawObjectObservation(
        ObjectCoverageModel Model,
        ObjectCoverageEntity Entity,
        List<RawMemberObservation> Members);

    private sealed record RawMemberObservation(
        MemberCoverageModel Model,
        MemberCoverageEntity Entity);

    private readonly record struct ObjectMatch(Models.Code.ObjectModel? Match, bool IsAmbiguous);

    private readonly record struct MemberMatch(Models.Code.MemberModel? Match, bool IsAmbiguous);

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

    private static string NormalizeMemberName(string value)
    {
        var trimmedValue = value.Trim();
        if (trimmedValue.StartsWith("<", StringComparison.Ordinal)) return string.Empty;

        if (trimmedValue.Equals(".ctor", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.Equals(".cctor", StringComparison.OrdinalIgnoreCase))
            return trimmedValue;

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
        if (trimmedValue.Equals(".ctor", StringComparison.OrdinalIgnoreCase))
            return member.Kind.Equals("constructor", StringComparison.OrdinalIgnoreCase);
        if (trimmedValue.Equals(".cctor", StringComparison.OrdinalIgnoreCase))
            return member.Kind.Equals("static_constructor", StringComparison.OrdinalIgnoreCase);

        if (trimmedValue.StartsWith("get_", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.StartsWith("set_", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.StartsWith("init_", StringComparison.OrdinalIgnoreCase))
            return member.Kind.Equals("property", StringComparison.OrdinalIgnoreCase) ||
                   member.Kind.Equals("indexer", StringComparison.OrdinalIgnoreCase);

        return !member.Kind.Equals("property", StringComparison.OrdinalIgnoreCase) &&
               !member.Kind.Equals("indexer", StringComparison.OrdinalIgnoreCase) &&
               !member.Kind.Equals("constructor", StringComparison.OrdinalIgnoreCase) &&
               !member.Kind.Equals("static_constructor", StringComparison.OrdinalIgnoreCase);
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

    private static ObjectMatch SelectBestObjectMatch(
        IReadOnlyCollection<Models.Code.ObjectModel> candidates,
        ObjectCoverageModel objectCoverage,
        IReadOnlyDictionary<int, List<Models.Code.MemberModel>> membersByObjectId,
        IReadOnlyDictionary<int, string> filePathById,
        string normalizedCoverageName,
        string normalizedCoverageFilename)
    {
        if (candidates.Count == 0) return new ObjectMatch(null, false);

        var normalizedCoverageMembers = objectCoverage.Methods
            .Select(x => x.Name)
            .Select(NormalizeMemberName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scored = candidates
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
            .ToList();
        var bestScore = scored[0].Score;
        return scored.Count(x => x.Score == bestScore) > 1
            ? new ObjectMatch(null, true)
            : new ObjectMatch(scored[0].Candidate, false);
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

        if (CoverageTypeName.Normalize(BuildQualifiedObjectName(candidate))
            .Equals(normalizedCoverageName, StringComparison.OrdinalIgnoreCase)) score += 100;

        if (CoverageTypeName.SimpleName(candidate.Name)
            .Equals(CoverageTypeName.SimpleName(normalizedCoverageName), StringComparison.OrdinalIgnoreCase)) score += 60;

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
        AddCandidates(candidates, objectsBySimpleName, CoverageTypeName.SimpleName(normalizedCoverageName));

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

    private static MemberMatch FindMember(
        string normalizedMemberName,
        string coverageMemberName,
        MemberCoverageModel coverage,
        IReadOnlyCollection<Models.Code.MemberModel> objectMembers,
        IReadOnlyDictionary<string, List<Models.Code.MemberModel>>? objectMembersByName)
    {
        var matches = objectMembersByName != null &&
                      objectMembersByName.TryGetValue(normalizedMemberName, out var namedMatches)
            ? namedMatches.Where(x => IsCoverageCompatible(x, coverageMemberName)).ToList()
            : objectMembers.Where(x =>
            x.Name.Equals(normalizedMemberName, StringComparison.OrdinalIgnoreCase) &&
            IsCoverageCompatible(x, coverageMemberName)).ToList();

        if (matches.Count == 0) return new MemberMatch(null, false);
        if (matches.Count == 1) return new MemberMatch(matches[0], false);

        var byLine = matches.Where(member => CoverageLinesOverlapMember(coverage, member)).ToList();
        if (byLine.Count == 1) return new MemberMatch(byLine[0], false);

        var coverageParameterCount = CountParameters(coverage.Signature);
        var signatureCandidates = byLine.Count > 1 ? byLine : matches;
        var bySignature = signatureCandidates.Where(member =>
            CountParameters(member.FullString) == coverageParameterCount).ToList();
        return bySignature.Count == 1
            ? new MemberMatch(bySignature[0], false)
            : new MemberMatch(null, true);
    }

    internal static bool CoverageLinesOverlapMember(
        MemberCoverageModel coverage,
        Models.Code.MemberModel member)
    {
        var coverageStartLine = member.Location.StartLineNumber + 1;
        var coverageEndLine = member.Location.EndLineNumber + 1;
        return coverage.Lines.Any(line =>
            line.Number >= coverageStartLine &&
            line.Number <= coverageEndLine);
    }

    private static int CountParameters(string signature)
    {
        var start = signature.IndexOf('(');
        var end = signature.LastIndexOf(')');
        if (start < 0 || end <= start + 1) return 0;

        var content = signature[(start + 1)..end].Trim();
        if (content.Length == 0) return 0;

        var depth = 0;
        var count = 1;
        foreach (var character in content)
        {
            if (character is '<' or '[' or '(') depth++;
            else if (character is '>' or ']' or ')') depth--;
            else if (character == ',' && depth == 0) count++;
        }

        return count;
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
