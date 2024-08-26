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

 [Fact]
        public void Constructor_ShouldInitializeFieldsCorrectly()
        {
            // Arrange
            var syntaxTrees = new Dictionary<string, SyntaxTree>
            {
                { "tree1", SyntaxFactory.ParseSyntaxTree("class C { }") }
            };
            var projectReferences = new List<string> { "Reference1", "Reference2" };
            var assemblies = new List<MetadataReference> { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
            var documents = new List<string> { "Doc1", "Doc2" };
            var projectFilePath = "path/to/project.csproj";

            // Act
            var analysisProject = new AnalysisProject(syntaxTrees, projectReferences, assemblies, documents, projectFilePath);

            // Assert
            Assert.Equal(syntaxTrees, analysisProject.SyntaxTrees);
            Assert.Equal(projectReferences, analysisProject.ProjectReferences);
            Assert.Equal(assemblies, analysisProject.Assemblies);
            Assert.Equal(documents, analysisProject.Documents);
            Assert.Equal(projectFilePath, analysisProject.ProjectFilePath);
        }

        [Fact]
        public void Constructor_ShouldInitializeWithDefaultValues_WhenNoArgumentsProvided()
        {
            // Arrange
            var syntaxTrees = new Dictionary<string, SyntaxTree>();
            var projectReferences = new List<string>();
            var assemblies = new List<MetadataReference>();
            var documents = new List<string>();

            // Act
            var analysisProject = new AnalysisProject(syntaxTrees, projectReferences, assemblies, documents);

            // Assert
            Assert.Equal(syntaxTrees, analysisProject.SyntaxTrees);
            Assert.Equal(projectReferences, analysisProject.ProjectReferences);
            Assert.Equal(assemblies, analysisProject.Assemblies);
            Assert.Equal(documents, analysisProject.Documents);
            Assert.Equal("", analysisProject.ProjectFilePath);
        }

        [Fact]
        public void Constructor_ShouldInitializeWithProvidedProjectFilePath()
        {
            // Arrange
            var syntaxTrees = new Dictionary<string, SyntaxTree>();
            var projectReferences = new List<string>();
            var assemblies = new List<MetadataReference>();
            var documents = new List<string>();
            var projectFilePath = "path/to/project.csproj";

            // Act
            var analysisProject = new AnalysisProject(syntaxTrees, projectReferences, assemblies, documents, projectFilePath);

            // Assert
            Assert.Equal(projectFilePath, analysisProject.ProjectFilePath);
        }
}