/*
 * consulthunter
 * 2025-03-26
 *
 * An abstraction of a Class
 *
 * ClassModel.cs
 */

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
    string name = "",
    List<string>? attributes = null,
    List<string>? modifiers = null,
    List<string>? classFields = null,
    Location? location = null,
    string classBody = "")
{
    public string Name {get; set; } = name;
    public List<string> Attributes { get; set; } = attributes ?? new List<string>();
    public List<string> Modifiers { get; set; } = modifiers ?? new List<string>();
    public List<string> ClassFields { get; set; } = classFields ?? new List<string>();
    public Location Location { get; set; } = location ?? new Location(0,0);
    public string ClassBody { get; set; } = classBody;
}