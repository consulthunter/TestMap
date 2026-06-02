using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Mapping.Experiment;

public static class SourceTestMappingMappingExtensions
{
    public static SourceTestMappingItem ToDomain(this SourceTestMappingEntity entity)
    {
        return new SourceTestMappingItem
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            SourceMemberId = entity.SourceMemberId,
            TestMemberId = entity.TestMemberId,
            EvidenceKind = entity.EvidenceKind,
            IsGrounded = entity.IsGrounded,
            AccessPathStrategy = entity.AccessPathStrategy,
            PathLength = entity.PathLength,
            Confidence = entity.Confidence,
            Summary = entity.Summary,
            ResolverVersion = entity.ResolverVersion,
            CreatedAt = entity.CreatedAt,
            TraceSteps = entity.TraceSteps
                .OrderBy(x => x.StepIndex)
                .Select(x => x.ToDomain())
                .ToList()
        };
    }
}
