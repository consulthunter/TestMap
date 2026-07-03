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
            LinesCovered = entity.LinesCovered,
            LinesValid = entity.LinesValid,
            BranchesCovered = entity.BranchesCovered,
            BranchesValid = entity.BranchesValid
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
            LinesCovered = model.LinesCovered,
            LinesValid = model.LinesValid,
            BranchesCovered = model.BranchesCovered,
            BranchesValid = model.BranchesValid,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static double SanitizeDouble(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }
}