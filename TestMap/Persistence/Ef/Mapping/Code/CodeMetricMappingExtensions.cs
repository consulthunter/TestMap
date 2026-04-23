using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Mapping.Code;

public static class CodeMetricMappingExtensions
{
    public static CodeMetricEntity ToEntity(this CodeMetricsModel model, int entityId, string entityType)
    {
        return new CodeMetricEntity
        {
            EntityId = entityId,
            EntityType = entityType,
            MaintainabilityIndex = model.MaintainabilityIndex,
            CyclomaticComplexity = model.CyclomaticComplexity,
            ClassCoupling = model.ClassCoupling,
            DepthOfInheritance = model.DepthOfInheritance,
            SourceLinesOfCode = model.SourceLinesOfCode,
            ExecutableLinesOfCode = model.ExecutableLinesOfCode
        };
    }
    public static CodeMetricsModel ToDomain(this CodeMetricEntity model)
    {
        return new CodeMetricsModel(
            entityType: model.EntityType,
            id: model.Id,
            entityId: model.EntityId,
            maintainabilityIndex: model.MaintainabilityIndex,
            cyclomaticComplexity: model.CyclomaticComplexity,
            classCoupling: model.ClassCoupling,
            depthOfInheritance: model.DepthOfInheritance,
            sourceLinesOfCode: model.SourceLinesOfCode,
            executableLinesOfCode: model.ExecutableLinesOfCode
        );
    }
}
