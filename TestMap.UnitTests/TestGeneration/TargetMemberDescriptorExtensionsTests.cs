using TestMap.Models.Experiment;
using TestMap.Models.Code;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.TestGeneration;

public sealed class TargetMemberDescriptorExtensionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ContainingTypeIdentity_ResolvesNestedGenericTypePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nested-type-{Guid.NewGuid():N}.cs");
        try
        {
            File.WriteAllText(path, """
                namespace Demo;
                public class Outer<T>
                {
                    public class Inner<TValue>
                    {
                        public void Target() { }
                    }
                }
                """);
            var sourceObject = new ObjectModel(
                [],
                [],
                new Location(3, 0, 6, 1),
                @namespace: "Demo",
                name: "Inner");

            var identity = ContainingTypeIdentity.Resolve(sourceObject, path);

            Assert.Equal("Demo.Outer.Inner", identity);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToTargetMemberDescriptor_UsesQualifiedTypeIdentityInsteadOfClassSource()
    {
        var candidate = new CandidateMethod
        {
            MemberId = 14,
            MethodName = "RemoveStudent",
            Signature = "public void RemoveStudent(TextReader reader, TextWriter writer)"
        };
        var context = new CandidateMethodContext
        {
            Method = candidate,
            MethodSignature = candidate.Signature,
            ContainingClass = "public class StudentList { /* source context */ }",
            ContainingType = "TestMap_Example.StudentList",
            TestNamespace = "TestMap_Example.Tests",
            TestClassName = "StudentListTest",
            TestFilePath = "tests/StudentListTest.cs",
            SourceFilePath = "src/StudentList.cs",
            SourceLocation = new CandidateSourceLocation
            {
                SourceFilePath = "src/StudentList.cs",
                StartLine = 82,
                EndLine = 95
            },
            SourceProjectPath = "src/TestMap_Example.csproj",
            TestProjectPath = "tests/TestMap_Example.Tests.csproj",
            TargetBuildFramework = "net10.0",
            SolutionFilePath = "TestMap_Example.sln",
            ExampleTest = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = string.Empty,
            TestFileContents = string.Empty,
            TestSupportContext = string.Empty,
            TestFramework = "xUnit",
            TestDependencies = string.Empty,
            CoverageGapSummary = string.Empty
        };

        var descriptor = context.ToTargetMemberDescriptor();

        Assert.Equal("TestMap_Example.StudentList", descriptor.ContainingType);
        Assert.DoesNotContain("public class", descriptor.ContainingType);
    }
}
