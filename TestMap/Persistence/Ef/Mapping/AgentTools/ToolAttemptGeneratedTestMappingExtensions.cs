using TestMap.Models.AgentTools;
using TestMap.Persistence.Ef.Entities.AgentTools;

namespace TestMap.Persistence.Ef.Mapping.AgentTools;

public static class ToolAttemptGeneratedTestMappingExtensions
{
    public static ToolAttemptGeneratedTest ToDomain(this ToolAttemptGeneratedTestEntity entity) => new()
    {
        Id = entity.Id,
        ToolAttemptId = entity.ToolAttemptId,
        MemberId = entity.MemberId,
        MappingId = entity.MappingId
    };

    public static ToolAttemptGeneratedTestEntity ToEntity(this ToolAttemptGeneratedTest row) => new()
    {
        Id = row.Id,
        ToolAttemptId = row.ToolAttemptId,
        MemberId = row.MemberId,
        MappingId = row.MappingId
    };
}
