using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Mappings;

public static class ObjectCoverageMappingExtensions
{
    public static ObjectCoverageModel ToDomain(this ObjectCoverageEntity entity)
    {
        return new ObjectCoverageModel
        {
            LineRate = entity.LineRate,
            BranchRate = entity.BranchRate,
            ComplexityRaw = entity.Complexity.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public static ObjectCoverageEntity ToEntity(this ObjectCoverageModel model, int objectId, int coverageReportId)
    {
        return new ObjectCoverageEntity
        {
            ObjectId = objectId,
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
