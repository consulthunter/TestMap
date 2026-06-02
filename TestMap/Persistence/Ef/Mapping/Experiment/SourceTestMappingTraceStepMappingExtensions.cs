using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Mapping.Experiment;

public static class SourceTestMappingTraceStepMappingExtensions
{
    public static SourceTestMappingTraceStepItem ToDomain(this SourceTestMappingTraceStepEntity entity)
    {
        return new SourceTestMappingTraceStepItem
        {
            Id = entity.Id,
            SourceTestMappingId = entity.SourceTestMappingId,
            StepIndex = entity.StepIndex,
            FromMemberId = entity.FromMemberId,
            ToMemberId = entity.ToMemberId,
            RelationshipKind = entity.RelationshipKind,
            EdgeSource = entity.EdgeSource,
            Summary = entity.Summary
        };
    }
}
