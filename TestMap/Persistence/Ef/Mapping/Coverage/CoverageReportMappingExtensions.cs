using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Mappings;

public static class CoverageReportMappingExtensions
{
    public static CoverageReportModel ToDomain(this CoverageReportEntity entity)
    {
        return new CoverageReportModel
        {
            LineRate = entity.LineRate,
            BranchRate = entity.BranchRate,
            ComplexityRaw = entity.Complexity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Version = entity.Version,
            Timestamp = entity.Timestamp,
            RunId = entity.RunId,
            CollectionStatus = entity.CollectionStatus,
            CollectionReason = entity.CollectionReason,
            SuccessfulCollector = entity.SuccessfulCollector,
            CollectionMetadataJson = entity.CollectionMetadataJson,
            HasUsableCoverage = entity.HasUsableCoverage,
            LineCountsAvailable = entity.LineCountsAvailable,
            BranchCountsAvailable = entity.BranchCountsAvailable,
            MeasurementPolicyVersion = entity.MeasurementPolicyVersion,
            RawObjectCount = entity.RawObjectCount,
            MappedObjectCount = entity.MappedObjectCount,
            RawMemberCount = entity.RawMemberCount,
            MappedMemberCount = entity.MappedMemberCount,
            LinesCovered = entity.LinesCovered,
            LinesValid = entity.LinesValid,
            BranchesCovered = entity.BranchesCovered,
            BranchesValid = entity.BranchesValid,
            ScopeKind = entity.ScopeKind,
            ReportRole = entity.ReportRole,
            ExperimentRunId = entity.ExperimentRunId,
            SourceProjectPath = entity.SourceProjectPath,
            TestProjectPath = entity.TestProjectPath,
            TargetFramework = entity.TargetFramework
        };
    }

    public static CoverageReportEntity ToEntity(this CoverageReportModel model, int projectId)
    {
        return new CoverageReportEntity
        {
            ProjectId = projectId,
            LineRate = SanitizeDouble(model.LineRate),
            BranchRate = SanitizeDouble(model.BranchRate),
            Complexity = SanitizeDouble(model.ComplexityValue),
            Version = model.Version,
            Timestamp = model.Timestamp,
            RunId = model.RunId,
            CollectionStatus = model.CollectionStatus,
            CollectionReason = model.CollectionReason,
            SuccessfulCollector = model.SuccessfulCollector,
            CollectionMetadataJson = model.CollectionMetadataJson,
            HasUsableCoverage = model.HasUsableCoverage,
            LineCountsAvailable = model.LineCountsAvailable,
            BranchCountsAvailable = model.BranchCountsAvailable,
            MeasurementPolicyVersion = model.MeasurementPolicyVersion,
            RawObjectCount = model.RawObjectCount,
            MappedObjectCount = model.MappedObjectCount,
            RawMemberCount = model.RawMemberCount,
            MappedMemberCount = model.MappedMemberCount,
            LinesCovered = model.LinesCovered,
            LinesValid = model.LinesValid,
            BranchesCovered = model.BranchesCovered,
            BranchesValid = model.BranchesValid,
            CreatedAt = DateTime.UtcNow,
            ScopeKind = model.ScopeKind,
            ReportRole = model.ReportRole,
            ExperimentRunId = model.ExperimentRunId,
            SourceProjectPath = model.SourceProjectPath,
            TestProjectPath = model.TestProjectPath,
            TargetFramework = model.TargetFramework
        };
    }

    private static double SanitizeDouble(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }
}
