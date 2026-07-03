namespace TestMap.Models.AgentTools;

/// <summary>
/// Links a <see cref="ToolAttempt"/> to a test member discovered in the workspace after
/// the tool run. Populated from <c>changed-files.txt</c> + the source-test mapping refresh
/// that runs immediately after a completed tool attempt with changes.
/// </summary>
public sealed class ToolAttemptGeneratedTest
{
    public int Id { get; set; }
    public int ToolAttemptId { get; set; }

    /// <summary>The member id of the test method added or modified by the tool.</summary>
    public int MemberId { get; set; }

    /// <summary>
    /// The source-test mapping id that links this test member to the candidate source member,
    /// when the analysis refresh was able to establish the mapping. Null when no mapping exists.
    /// </summary>
    public int? MappingId { get; set; }
}
