using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Mappings;

public static class MemberCoverageMappingExtensions
{
    public static MemberCoverageModel ToDomain(this MemberCoverageEntity entity)
    {
        return new MemberCoverageModel
        {
            LineRate = entity.LineRate,
            BranchRate = entity.BranchRate,
            ComplexityRaw = entity.Complexity.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public static MemberCoverageEntity ToEntity(this MemberCoverageModel model, int memberId, int coverageReportId)
    {
        return new MemberCoverageEntity
        {
            MemberId = memberId,
            CoverageReportId = coverageReportId,
            LineRate = SanitizeDouble(model.LineRate),
            BranchRate = SanitizeDouble(model.BranchRate),
            Complexity = SanitizeDouble(model.ComplexityValue)
        };
    }

    private static double SanitizeDouble(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }
}