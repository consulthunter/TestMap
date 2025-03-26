/*
 * consulthunter
 * 2025-03-26
 *
 * An abstraction for a method
 *
 * MethodModel.cs
 */
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
    string name = "",
    List<string>? attributes = null,
    List<string>? modifiers = null,
    List<string>? invocations = null,
    string methodBody = "",
    Location? location =  null)
{
    public string Name { get; set; } = name;
    public List<string> Attributes { get; set; } = attributes ?? new List<string>();
    public List<string> Modifiers { get; set; } = modifiers ?? new List<string>();
    public List<string> Invocations { get; set; } = invocations ?? new List<string>();
    public string MethodBody { get; set; } = methodBody;
    public Location Location = location ?? new Location(0,0);
    
}