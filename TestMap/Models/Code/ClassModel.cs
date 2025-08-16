/*
 * consulthunter
 * 2025-03-26
 *
 * An abstraction of a Class
 *
 * ClassModel.cs
 */

using System.Text.Json;

namespace TestMap.Models.Code;

/// <summary>
///   Representation of a single class
/// </summary>
/// <param name="name">Name of the class</param>
/// <param name="attributes">Attributes for the class</param>
/// <param name="modifiers">Modifiers on the class</param>
/// <param name="classFields">Fields/Properties for the class</param>
/// <param name="location">Location of the class in the tree</param>
/// <param name="classBody">Complete body of the class</param>
public class ClassModel(
    int fileId = 0,
    string guid = "",
    string name = "",
    string visibility = "",
    List<string>? attributes = null,
    List<string>? modifiers = null,
    string fullString = "",
    string docString = "",
    bool isTestClass = false,
    string testingFramework = "",
    Location? location = null)
{
    public int Id { get; set; } = 0;
    public int FileId { get; set; } = fileId;
    public string Guid { get; set; } = guid;
    public string Name {get; set; } = name;
    public string Visibility { get; set; } = visibility;
    public string Modifiers { get; set; } = JsonSerializer.Serialize(modifiers) ?? JsonSerializer.Serialize(new List<string>());
    public string Attributes { get; set; } = JsonSerializer.Serialize(attributes) ?? JsonSerializer.Serialize(new List<string>());
    public string FullString { get; set; } = fullString;
    public string DocString { get; set; } = docString;
    public bool IsTestClass { get; set; } = isTestClass;
    public string TestingFramework { get; set; } = testingFramework;
    public Location Location { get; set; } = location ?? new Location(0,0,0,0);
}