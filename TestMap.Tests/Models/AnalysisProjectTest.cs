﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TestMap.Models;
using Xunit;

namespace TestMap.Tests.Models;

[TestSubject(typeof(AnalysisProject))]
public class AnalysisProjectTest
{
    private readonly string _solutionPath;
    private readonly Dictionary<string, SyntaxTree> _syntaxTrees;
    private readonly List<string> _projectReferences;
    private readonly List<MetadataReference> _assemblyReferences;
    private readonly string _projectFilePath;
    private readonly CSharpCompilation _compilation;
    private readonly string _languageFramework;

    public AnalysisProjectTest()
    {
        _solutionPath = "solution.sln";
        _syntaxTrees = new Dictionary<string, SyntaxTree>();
        _projectReferences = new List<string>();
        _assemblyReferences = new List<MetadataReference>();
        _projectFilePath = "example.csproj";
        _languageFramework = "8.0";
        _compilation = CSharpCompilation.Create("example", _syntaxTrees.Values);
    }

    private AnalysisProject CreateAnalysisProject()
    {
        return new AnalysisProject(_solutionPath, _syntaxTrees, _projectReferences, 
            _assemblyReferences, _projectFilePath, _compilation, _languageFramework);
    }
    
    [Fact]
    public void Constructor_ShouldInitializeAnalysisProject()
    {
        // Arrange
        var analysisProject = CreateAnalysisProject();

        // Assert
        Assert.Equal(_solutionPath, analysisProject.SolutionFilePath);
        Assert.Equal(_syntaxTrees, analysisProject.SyntaxTrees);
        Assert.Equal(_projectReferences, analysisProject.ProjectReferences);
        Assert.Equal(_assemblyReferences, analysisProject.Assemblies);
        Assert.Equal(_projectFilePath, analysisProject.ProjectFilePath);
        Assert.Equal(_compilation, analysisProject.Compilation);
        Assert.Equal(_languageFramework, analysisProject.LanguageFramework);
        
    }
}