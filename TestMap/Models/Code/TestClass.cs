/*
 * consulthunter
 * 2025-03-26
 *
 * An abstraction of a TestClass
 * A class that contains tests
 *
 * TestClass.cs
 */

namespace TestMap.Models.Code;

/// <summary>
///     TestClass
///     A single test class and any source code class (class being tested)
/// </summary>
public class TestClass(
    string name = "",
    List<string>? attributes = null,
    List<string>? modifiers = null,
    List<string>? classFields = null,
    Location? location = null,
    string classBody = "",
    List<NonTestMethod>? nonTestMethods = null,
    string testFramework = "",
    List<TestMethod>? testMethod = null,
    List<NonTestClass>? sourceClasses = null)
    : ClassModel(name, attributes, modifiers, classFields, location, classBody)
{
    public string TestFramework { get; set; } = testFramework;
    public List<TestMethod> TestMethods { get; set; } = testMethod ?? new List<TestMethod>();
    public List<NonTestMethod> NonTestMethods { get; set; } = nonTestMethods ?? new List<NonTestMethod>();
    public List<NonTestClass> SourceClasses { get; set; } = sourceClasses ?? new List<NonTestClass>();

    public void AddIfNotPresent(NonTestClass newItem)
    {
        // Check if an item with the specified name already exists
        var exists = SourceClasses.Any(x => x.Name.Trim() == newItem.Name.Trim());

        // Add the item if it doesn't exist
        if (!exists)
        {
            SourceClasses.Add(newItem);
        }
    }
}