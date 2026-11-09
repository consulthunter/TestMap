using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Mappings;

public static class MemberCoverageMappingExtensions
{
    public static MemberCoverageModel ToDomain(this MemberCoverageEntity entity)
    {
        return new MemberCoverageModel
        {
            MemberId = entity.MemberId,
            CoverageReportId = entity.CoverageReportId,
            ObjectCoverageId = entity.ObjectCoverageId,
            SourceOrdinal = entity.SourceOrdinal,
            Name = entity.Name,
            Signature = entity.Signature,
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

    public static MemberCoverageEntity ToEntity(this MemberCoverageModel model, int? memberId, int coverageReportId)
    {
        return new MemberCoverageEntity
        {
            MemberId = memberId,
            CoverageReportId = coverageReportId,
            ObjectCoverageId = model.ObjectCoverageId,
            SourceOrdinal = model.SourceOrdinal,
            Name = model.Name,
            Signature = model.Signature,
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
