/*
 * consulthunter
 * 2025-03-26
 *
 * An abstraction of a TestMethod
 * A method that is a test
 *
 * TestMethod.cs
 */

namespace TestMap.Models.Code;

/// <summary>
///     Representation of a single test
/// </summary>
/// <param name="name">Name of the test</param>
/// <param name="attributes">Any attributes for the test</param>
/// <param name="modifiers">Modifiers for the test</param>
/// <param name="methodInvocations">Methods invoked in the test</param>
/// <param name="methodBody">Complete body of the method</param>
/// <param name="location">Location within the tree i.e. (Start, End)</param>
/// <param name="assertions">Assertions found in the test.</param>
public class TestMethod(
    string name,
    List<string> attributes,
    List<string> modifiers,
    List<string> methodInvocations,
    string methodBody,
    Location location,
    List<string> assertions)
    : MethodModel(name, attributes, modifiers, methodInvocations, methodBody, location)
{
    // Methods invoked in the test that could be resolved
    // with semantic modeling, giving us the source code for the invocation
    public List<NonTestMethod> SourceMethods { get; set; } = new List<NonTestMethod>();
    public List<string> Assertions { get; set; } = assertions;
    
    public void AddIfNotPresent(NonTestMethod newItem)
    {
        // Check if an item with the specified name already exists
        var exists = SourceMethods.Any(x => x.Name.Trim() == newItem.Name.Trim());

        // Add the item if it doesn't exist
        if (!exists)
        {
            SourceMethods.Add(newItem);
        }
    }
}