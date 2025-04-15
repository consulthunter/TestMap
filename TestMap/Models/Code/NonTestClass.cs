/*
 * consulthunter
 * 2025-03-26
 *
 * An abstraction of a Non-Test Class
 * A class that does not contain tests
 *
 * NonTestClass.cs
 */

using TestMap.Models.Coverage;

namespace TestMap.Models.Code;

/// <summary>
///     Representation of a class that
///     is not a test class
///
///     Classes that contain code called
///     within a test
/// </summary>
/// <param name="name">Name of the class</param>
/// <param name="filePath">Filepath for the class</param>
/// <param name="ns">Namespace of the class</param>
/// <param name="usingStatements">Using statements in the class</param>
/// <param name="attributes">Attributes for the class</param>
/// <param name="modifiers">Modifiers for the class</param>
/// <param name="classFields">Fields/Properties in the class</param>
/// <param name="location">Location of the class in the tree</param>
/// <param name="classBody">Complete body of the class</param>
/// <param name="coverage">Coverage (Cobertura) metrics found for the class</param>
public class NonTestClass(
    string name = "",
    string filePath = "",
    string ns = "",
    List<string>? usingStatements = null,
    List<string>? attributes = null,
    List<string>? modifiers = null,
    List<string>? classFields = null,
    Location? location = null,
    string classBody = "",
    ClassCoverage? coverage = null)
    : ClassModel(name, attributes, modifiers, classFields, location, classBody)
{
    public string FilePath { get; set; } = filePath;
    
    public string Namespace { get; set; } = ns;
    
    public List<string> UsingStatements { get; set; } = usingStatements ?? new List<string>();

    public ClassCoverage Coverage { get; set; } = coverage ?? new ClassCoverage();
}