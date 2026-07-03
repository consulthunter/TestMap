using TestMap.Persistence.Ef.Entities.AgentTools;

namespace TestMap.Persistence.Ef.Entities.AgentTools;

/// <summary>
/// Linking row between a <see cref="ToolAttemptEntity"/> and a test member that was added
/// or modified by that tool run. Populated from the analysis refresh that runs after a
/// completed tool attempt with changed files.
/// </summary>
public class ToolAttemptGeneratedTestEntity
{
    public int Id { get; set; }
    public int ToolAttemptId { get; set; }
    public int MemberId { get; set; }
    public int? MappingId { get; set; }

    public virtual ToolAttemptEntity? ToolAttempt { get; set; }
}
