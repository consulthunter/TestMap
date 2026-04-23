using TestMap.Models.Code;

namespace TestMap.Persistence.Ef.Entities.Code;

public class MemberEntity
{
    public int Id { get; set; }
    public int ObjectEntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public List<string> Modifiers { get; set; } = new List<string>();
    public List<string> Attributes { get; set; } = new List<string>();
    public string DocString { get; set; } = string.Empty;
    public string FullString { get; set; } = string.Empty;
    public bool IsTestMember { get; set; }
    public bool IsGenerated { get; set; }
    public List<string> TestCategories { get; set; } = new List<string>();
    public string TestIntent { get; set; } = string.Empty;
    public string TestMetadataSource { get; set; } = string.Empty;
    public double? TestMetadataConfidence { get; set; }
    public string TestMetadataPromptVersion { get; set; } = string.Empty;
    public Location Location { get; set; } = new Location(0, 0, 0, 0);
    public string ContentHash { get; set; } = string.Empty;
    
}
