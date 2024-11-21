/*
 * consulthunter
 * 2024-11-07
 * Abstraction for CSharp Solutions
 * that are being analyzed
 * AnalysisSolution.cs
 */

using Microsoft.CodeAnalysis;

namespace TestMap.Models;

/// <summary>
/// AnalysisSolution
/// Representation of a single solution (.sln)
/// found in the repository and projects contained in
/// the solution
/// </summary>
/// <param name="solution">Solution (.sln) found within the repo</param>
/// <param name="projects">Projects (.csproj) found contained in that solution</param>
public class AnalysisSolution(Solution solution, List<string> projects)
{
    public readonly Solution Solution = solution;

    public readonly List<string> Projects = projects;
}