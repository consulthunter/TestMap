namespace TestMap.Models.Code;

public class ObjectModel(
    List<string> attributes,
    List<string> modifiers,
    Location location,
    int id = 0,
    int fileId = 0,
    string @namespace = "",
    string name = "",
    string kind = "",
    string docString = "",
    string fullString = "",
    bool isTestObject = false,
    string testFramework = "")
{
    public int Id { get; set; } = id;
    public int FileId { get; set; } = fileId;
    public string Namespace { get; set; } = @namespace;
    public string Name { get; set; } = name;
    public string Kind { get; set; } = kind;
    public List<string> Modifiers { get; set; } = modifiers;
    public List<string> Attributes { get; set; } = attributes;
    public string DocString { get; set; } = docString;
    public string FullString { get; set; } = fullString;
    public bool IsTestObject{ get; set; } = isTestObject;
    public string TestFramework { get; set; } = testFramework;
    public Location Location { get; set; } = location;
    public string ContentHash => Utilities.Utilities.ComputeObjectIdentityHash(FileId, Namespace, Name, Kind);
}
