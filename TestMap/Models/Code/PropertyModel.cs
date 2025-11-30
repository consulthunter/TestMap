using System.Text.Json;

namespace TestMap.Models.Code;

public class PropertyModel(
    int classId = 0,
    string guid = "",
    string name = "",
    string visibility = "",
    List<string>? attributes = null,
    List<string>? modifiers = null,
    string fullString = "",
    Location? location = null)
{
    public int Id { get; set; } = 0;
    public int ClassId { get; set; } = classId;
    public string Guid { get; set; } = guid;
    public string Name { get; set; } = name;
    public string Visibility { get; set; } = visibility;

    public string Modifiers { get; set; } =
        JsonSerializer.Serialize(modifiers) ?? JsonSerializer.Serialize(new List<string>());

    public string Attributes { get; set; } =
        JsonSerializer.Serialize(attributes) ?? JsonSerializer.Serialize(new List<string>());

    public string FullString { get; set; } = fullString;
    public Location Location { get; set; } = location ?? new Location(0, 0, 0, 0);
    
    public string ContentHash { get; set;} = Utilities.Utilities.ComputeSha256(fullString);
}