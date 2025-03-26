/*
 * consulthunter
 * 2025-03-26
 *
 * An abstraction of a NonTestMethod
 * A method that is not a test
 *
 * NonTestMethod.cs
 */
using TestMap.Models.Coverage;

namespace TestMap.Models.Code;

/// <summary>
/// 
/// </summary>
/// <param name="name"></param>
/// <param name="attributes"></param>
/// <param name="modifiers"></param>
/// <param name="invocations"></param>
/// <param name="methodBody"></param>
/// <param name="location"></param>
/// <param name="coverage"></param>
public class NonTestMethod(
    string name,
    List<string> attributes,
    List<string> modifiers,
    List<string> invocations,
    string methodBody,
    Location location,
    MethodCoverage coverage) : MethodModel(name,  attributes, modifiers, invocations, methodBody, location)
{
    public MethodCoverage MethodCoverage { get; set; } = coverage;
}