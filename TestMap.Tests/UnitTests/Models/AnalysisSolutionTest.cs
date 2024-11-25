using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using TestMap.Models;
using Xunit;

namespace TestMap.Tests.UnitTests.Models;

[TestSubject(typeof(AnalysisSolution))]
public class AnalysisSolutionTest
{
    private readonly List<string> _projects;
    private readonly Solution _solution;

    public AnalysisSolutionTest()
    {
        var tempSolution = MSBuildWorkspace.Create();
        _solution = tempSolution.CurrentSolution;
        _projects = new List<string>();
    }

    private AnalysisSolution CreateAnalysisSolution()
    {
        return new AnalysisSolution(_solution, _projects);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Constructor_ShouldInitializeAnalysisSolution()
    {
        var analysisSolution = CreateAnalysisSolution();

        Assert.Equal(_solution, analysisSolution.Solution);
        Assert.Equal(_projects, analysisSolution.Projects);
    }
}