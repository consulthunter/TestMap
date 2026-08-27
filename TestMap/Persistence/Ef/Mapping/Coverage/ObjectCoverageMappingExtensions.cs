using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Mappings;

public static class ObjectCoverageMappingExtensions
{
    public static ObjectCoverageModel ToDomain(this ObjectCoverageEntity entity)
    {
        return new ObjectCoverageModel
        {
            ObjectId = entity.ObjectId,
            CoverageReportId = entity.CoverageReportId,
            SourceOrdinal = entity.SourceOrdinal,
            PackageName = entity.PackageName,
            Name = entity.Name,
            Filename = entity.Filename,
            AttributionStatus = entity.AttributionStatus,
            AttributionReason = entity.AttributionReason,
            LineRate = entity.LineRate,
            BranchRate = entity.BranchRate,
            LinesCovered = entity.LinesCovered,
            LinesValid = entity.LinesValid,
            BranchesCovered = entity.BranchesCovered,
            BranchesValid = entity.BranchesValid,
            LineCountsAvailable = entity.LineCountsAvailable,
            BranchCountsAvailable = entity.BranchCountsAvailable,
            ComplexityRaw = entity.Complexity.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public static ObjectCoverageEntity ToEntity(this ObjectCoverageModel model, int? objectId, int coverageReportId)
    {
        return new ObjectCoverageEntity
        {
            ObjectId = objectId,
            CoverageReportId = coverageReportId,
            SourceOrdinal = model.SourceOrdinal,
            PackageName = model.PackageName,
            Name = model.Name,
            Filename = model.Filename,
            AttributionStatus = model.AttributionStatus,
            AttributionReason = model.AttributionReason,
            LineRate = SanitizeDouble(model.LineRate),
            BranchRate = SanitizeDouble(model.BranchRate),
            LinesCovered = model.LinesCovered,
            LinesValid = model.LinesValid,
            BranchesCovered = model.BranchesCovered,
            BranchesValid = model.BranchesValid,
            LineCountsAvailable = model.LineCountsAvailable,
            BranchCountsAvailable = model.BranchCountsAvailable,
            Complexity = SanitizeDouble(model.ComplexityValue)
        };
    }

    private static double SanitizeDouble(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }
}
