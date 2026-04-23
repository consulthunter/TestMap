using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;

namespace TestMap.Persistence.Ef.Mappings;

public static class CoverageGapMappingExtensions
{
    public static CoverageGapModel ToDomain(this CoverageGapEntity entity)
    {
        return new CoverageGapModel
        {
            Id = entity.Id,
            MemberId = entity.MemberId,
            CoverageReportId = entity.CoverageReportId,
            LineNumber = entity.LineNumber,
            Hits = entity.Hits,
            IsBranch = entity.IsBranch,
            ConditionCoverage = entity.ConditionCoverage,
            GapKind = Enum.TryParse<CoverageGapKind>(entity.GapKind, out var gapKind)
                ? gapKind
                : CoverageGapKind.UncoveredLine,
            SourceText = entity.SourceText,
            MemberContentHash = entity.MemberContentHash
        };
    }

    public static CoverageGapEntity ToEntity(this CoverageGapModel model)
    {
        return new CoverageGapEntity
        {
            Id = model.Id,
            MemberId = model.MemberId,
            CoverageReportId = model.CoverageReportId,
            LineNumber = model.LineNumber,
            Hits = model.Hits,
            IsBranch = model.IsBranch,
            ConditionCoverage = model.ConditionCoverage,
            GapKind = model.GapKind.ToString(),
            SourceText = model.SourceText,
            MemberContentHash = model.MemberContentHash
        };
    }
}
