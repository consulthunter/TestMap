using System.Collections.Generic;
using JetBrains.Annotations;
using TestMap.Models;
using Xunit;

namespace TestMap.Tests.Models;

[TestSubject(typeof(TestMethodRecord))]
public class TestMethodRecordTest
{

[Fact]
        public void TestMethod_CanBeInstantiated_WithConstructor()
        {
            // Arrange & Act
            var testMethod = new TestMethodRecord(
                "RepoName",
                "Path/To/File",
                "Namespace",
                "public class MyClass",
                new List<string> { "public int Id", "public string Name" },
                new List<string> { "using System;", "using System.Collections.Generic;" },
                "xUnit",
                "public void MyMethod() { /* ... */ }",
                new List<(string, string)> { ("MethodName1", "arg1"), ("MethodName2", "arg2") });

            // Assert
            Assert.NotNull(testMethod);
        }

        [Fact]
        public void TestMethod_Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange
            var repo = "RepoName";
            var filePath = "Path/To/File";
            var ns = "Namespace";
            var classDeclaration = "public class MyClass";
            var classFields = new List<string> { "public int Id", "public string Name" };
            var usingStatements = new List<string> { "using System;", "using System.Collections.Generic;" };
            var framework = "xUnit";
            var methodBody = "public void MyMethod() { /* ... */ }";
            var methodInvocations = new List<(string, string)> { ("MethodName1", "arg1"), ("MethodName2", "arg2") };

            // Act
            var testMethod = new TestMethodRecord(
                repo,
                filePath,
                ns,
                classDeclaration,
                classFields,
                usingStatements,
                framework,
                methodBody,
                methodInvocations);

            // Assert
            Assert.Equal(repo, testMethod.Repo);
            Assert.Equal(filePath, testMethod.FilePath);
            Assert.Equal(ns, testMethod.Namespace);
            Assert.Equal(classDeclaration, testMethod.ClassDeclaration);
            Assert.Equal(classFields, testMethod.ClassFields);
            Assert.Equal(usingStatements, testMethod.UsingStatements);
            Assert.Equal(framework, testMethod.Framework);
            Assert.Equal(methodBody, testMethod.MethodBody);
            Assert.Equal(methodInvocations, testMethod.MethodInvocations);
        }

        [Fact]
        public void TestMethod_Constructor_DefaultLists()
        {
            // Arrange
            var repo = "RepoName";
            var filePath = "Path/To/File";
            var ns = "Namespace";
            var classDeclaration = "public class MyClass";

            // Act
            var testMethod = new TestMethodRecord(
                repo,
                filePath,
                ns,
                classDeclaration);

            // Assert
            Assert.NotNull(testMethod.ClassFields);
            Assert.Empty(testMethod.ClassFields);

            Assert.NotNull(testMethod.UsingStatements);
            Assert.Empty(testMethod.UsingStatements);

            Assert.NotNull(testMethod.MethodInvocations);
            Assert.Empty(testMethod.MethodInvocations);
        }
}