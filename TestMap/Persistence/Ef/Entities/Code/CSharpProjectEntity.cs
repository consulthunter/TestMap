using TestMap.Models.Code;

namespace TestMap.Persistence.Ef.Entities.Code;

public class CSharpProjectEntity
{
    public int Id { get; set; }
    public int SolutionId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public List<string> BuildTargets { get; set; } = new();
    public string DefaultBuildTarget { get; set; } = string.Empty;
    public ProjectBuildMetadataModel BuildMetadata { get; set; } = new();
    public string ContentHash { get; set; } = string.Empty;
}