/*
 * consulthunter
 * 2025-03-26
 *
 * An abstraction for a method
 *
 * MethodModel.cs
 */

using System.Text.Json;

namespace TestMap.Models.Code;

/// <summary>
///     Representation of a single method
/// </summary>
/// <param name="name">Name of the method</param>
/// <param name="attributes">Attributes for the method</param>
/// <param name="modifiers">Modifiers for the method</param>
/// <param name="invocations">Method invocations within the method</param>
/// <param name="methodBody">Complete body of the method</param>
/// <param name="location">Location of the method in the tree</param>
public class MethodModel(
    int classId = 0,
    string guid = "",
    string name = "",
    string visibility = "",
    List<string>? attributes = null,
    List<string>? modifiers = null,
    string fullString = "",
    string docString = "",
    bool isTestMethod = false,
    string testingFramework = "",
    Location? location =  null)
{
    public int Id { get; set; } = 0;
    public int ClassId { get; set; } = classId;
    public string Guid { get; set; } = guid;
    public string Name { get; set; } = name;
    public string Visibility { get; set; } = visibility;
    public string Modifiers { get; set; } = JsonSerializer.Serialize(modifiers) ?? JsonSerializer.Serialize(new List<string>());
    public string Attributes { get; set; } = JsonSerializer.Serialize(attributes) ?? JsonSerializer.Serialize(new List<string>());
    public string FullString { get; set; } = fullString;
    public string DocString { get; set; } = docString;
    public bool IsTestMethod { get; set; } = isTestMethod;
    public string TestingFramework { get; set; } = testingFramework;
    public string TestType { get; set; } = "";
    public Location Location = location ?? new Location(0,0, 0, 0);
    
}