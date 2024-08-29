using System.Collections.Generic;
using JetBrains.Annotations;
using TestMap.Models;
using Xunit;

namespace TestMap.Tests.Models;

[TestSubject(typeof(TestClassRecord))]
public class TestClassRecordTest
{

 [Fact]
        public void TestClass_CanBeInstantiated_WithConstructor()
        {
            // Arrange & Act
            var testClass = new TestClassRecord(
                "RepoName",
                "Path/To/File",
                "Namespace",
                "public class MyClass",
                new List<string> { "public int Id", "public string Name" },
                new List<string> { "using System;", "using System.Collections.Generic;" },
                "xUnit",
                "public void DoSomething() { /* ... */ }",
                "source code");

            // Assert
            Assert.NotNull(testClass);
        }

        [Fact]
        public void TestClass_Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange
            var repo = "RepoName";
            var filePath = "Path/To/File";
            var ns = "Namespace";
            var classDeclaration = "public class MyClass";
            var classFields = new List<string> { "public int Id", "public string Name" };
            var usingStatements = new List<string> { "using System;", "using System.Collections.Generic;" };
            var framework = "xUnit";
            var classBody = "public void DoSomething() { /* ... */ }";
            var sourceBody = "source code";

            // Act
            var testClass = new TestClassRecord(
                repo,
                filePath,
                ns,
                classDeclaration,
                classFields,
                usingStatements,
                framework,
                classBody,
                sourceBody);

            // Assert
            Assert.Equal(repo, testClass.Repo);
            Assert.Equal(filePath, testClass.FilePath);
            Assert.Equal(ns, testClass.Namespace);
            Assert.Equal(classDeclaration, testClass.ClassDeclaration);
            Assert.Equal(classFields, testClass.ClassFields);
            Assert.Equal(usingStatements, testClass.UsingStatements);
            Assert.Equal(framework, testClass.Framework);
            Assert.Equal(classBody, testClass.ClassBody);
            Assert.Equal(sourceBody, testClass.SourceBody);
        }

        [Fact]
        public void TestClass_Constructor_DefaultLists()
        {
            // Arrange
            var repo = "RepoName";
            var filePath = "Path/To/File";
            var ns = "Namespace";
            var classDeclaration = "public class MyClass";

            // Act
            var testClass = new TestClassRecord(
                repo,
                filePath,
                ns,
                classDeclaration);

            // Assert
            Assert.NotNull(testClass.ClassFields);
            Assert.Empty(testClass.ClassFields);

            Assert.NotNull(testClass.UsingStatements);
            Assert.Empty(testClass.UsingStatements);
        }
    }