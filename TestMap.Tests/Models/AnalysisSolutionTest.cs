using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using TestMap.Models;
using Xunit;

namespace TestMap.Tests.Models;

[TestSubject(typeof(AnalysisSolution))]
public class AnalysisSolutionTest
{

    [Fact]
    public void Constructor_ShouldInitializeFieldsCorrectly()
    {
        // Arrange
        var solution = CreateMockSolution(); // Method to create or mock a Solution instance
        var projects = new List<string> { "Project1", "Project2" };

        // Act
        var analysisSolution = new AnalysisSolution(solution, projects);

        // Assert
        Assert.Equal(solution, analysisSolution.Solution);
        Assert.Equal(projects, analysisSolution.Projects);
    }

    [Fact]
    public void Constructor_ShouldHandleEmptyProjectsList()
    {
        // Arrange
        var solution = CreateMockSolution(); // Method to create or mock a Solution instance
        var projects = new List<string>(); // Empty list

        // Act
        var analysisSolution = new AnalysisSolution(solution, projects);

        // Assert
        Assert.Equal(solution, analysisSolution.Solution);
        Assert.Empty(analysisSolution.Projects);
    }

    [Fact]
    public void Constructor_ShouldHandleNullProjectsList()
    {
        // Arrange
        var solution = CreateMockSolution(); // Method to create or mock a Solution instance
        List<string> projects = null; // Null list

        // Act
        var analysisSolution = new AnalysisSolution(solution, projects);

        // Assert
        Assert.Equal(solution, analysisSolution.Solution);
        Assert.Null(analysisSolution.Projects);
    }

    private Solution CreateMockSolution()
    {
        // Create a simple mock or a default instance of Solution
        return new AdhocWorkspace().CurrentSolution;
    }
}