using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Services.StaticAnalysis;
using Location = TestMap.Models.Code.Location;

namespace TestMap.UnitTests.StaticAnalysis;

public sealed class RoslynAnalysisUtilitiesTests
{
    // A generated test file routinely carries several hand-written helper classes alongside the
    // test class, and those helpers repeat the same member names -- an override of ReadLine, a
    // constructor, a Dispose. Members are unique only within their declaring type.
    private const string CollidingSource = """
                                           namespace Product.Tests;

                                           public class FirstReader
                                           {
                                               public string ReadLine() => "first";
                                           }

                                           public class SecondReader
                                           {
                                               public string ReadLine() => "second";
                                           }
                                           """;

    [Fact]
    [Trait("Category", "Unit")]
    public void FindMemberDeclaration_SameNameInAnotherType_ResolvesToTheDeclaringType()
    {
        var root = CSharpSyntaxTree.ParseText(CollidingSource).GetRoot();
        // A stale line number is the normal case: anything inserted above the member moves it,
        // and the persisted row still carries the position from the pass that recorded it.
        var member = CreateRow("ReadLine", "SecondReader", startLine: 0);

        var declaration = RoslynAnalysisUtilities.FindMemberDeclaration(root, member);

        var method = Assert.IsType<MethodDeclarationSyntax>(declaration);
        Assert.Equal(
            "SecondReader",
            method.Ancestors().OfType<ClassDeclarationSyntax>().First().Identifier.Text);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FindMemberDeclaration_LineNoLongerMatches_PicksTheNearestDeclaration()
    {
        var source = """
                     namespace Product.Tests;

                     public class Reader
                     {
                         public string ReadLine() => "only";
                     }
                     """;
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var member = CreateRow("ReadLine", "Reader", startLine: 999);

        var declaration = RoslynAnalysisUtilities.FindMemberDeclaration(root, member);

        Assert.NotNull(declaration);
        Assert.Equal("ReadLine", Assert.IsType<MethodDeclarationSyntax>(declaration).Identifier.Text);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FindMemberDeclaration_TypeAbsentFromFile_ReturnsNull()
    {
        var root = CSharpSyntaxTree.ParseText(CollidingSource).GetRoot();
        var member = CreateRow("ReadLine", "ThirdReader", startLine: 4);

        Assert.Null(RoslynAnalysisUtilities.FindMemberDeclaration(root, member));
    }

    private static RoslynMemberSymbolRow CreateRow(string name, string objectName, int startLine)
    {
        return new RoslynMemberSymbolRow(
            1,
            name,
            "method",
            $"public string {name}() => \"x\";",
            ["public"],
            true,
            false,
            "hash",
            objectName,
            "Product.Tests",
            true,
            "tests/Product.Tests/ReaderTests.cs",
            "tests/Product.Tests/Product.Tests.csproj",
            true,
            new Location(startLine, 0, startLine, 0));
    }
}
