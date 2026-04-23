namespace TestMap.Models.Code;

public class MemberModel(
    List<string> attributes,
    List<string> modifiers,
    List<string> testCategories,
    Location location,
    int id = 0,
    int objectEntityId = 0,
    string name = "",
    string kind = "",
    string docString = "",
    string fullString = "",
    bool isTestMember= false,
    string testIntent = "",
    bool isGenerated = false,
    string testMetadataSource = "",
    double? testMetadataConfidence = null,
    string testMetadataPromptVersion = "")
{
    public int Id { get; set; } = id;
    public int ObjectEntityId { get; set; } = objectEntityId;
    public string Name { get; set; } = name;
    public string Kind { get; set; } = kind;
    public List<string> Attributes { get; set; } = attributes;
    public List<string> Modifiers { get; set; } = modifiers;
    public string DocString { get; set; } = docString;
    public string FullString { get; set; } = fullString;
    public bool IsTestMember { get; set; } = isTestMember;
    public bool IsGenerated { get; set; } = isGenerated;
    public List<string> TestCategories { get; set; } = testCategories;
    public string TestIntent { get; set; } = testIntent;
    public string TestMetadataSource { get; set; } = testMetadataSource;
    public double? TestMetadataConfidence { get; set; } = testMetadataConfidence;
    public string TestMetadataPromptVersion { get; set; } = testMetadataPromptVersion;
    public Location Location { get; set; } = location;
    public string ContentHash => Utilities.Utilities.ComputeMemberIdentityHash(ObjectEntityId, Name, Kind, FullString);
}
