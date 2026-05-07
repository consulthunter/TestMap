using TestMap.Models.Code;

namespace TestMap.Persistence.Ef.Entities.Code;

public class ObjectEntity
{
    public int Id { get; set; }
    public int FileId { get; set; }
    public string Namespace { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public List<string> Modifiers { get; set; } = new();
    public List<string> Attributes { get; set; } = new();
    public string DocString { get; set; } = string.Empty;
    public string FullString { get; set; } = string.Empty;
    public bool IsTestObject { get; set; }
    public string TestFramework { get; set; } = string.Empty;
    public Location Location { get; set; } = new(0, 0, 0, 0);
    public string ContentHash { get; set; } = string.Empty;
}