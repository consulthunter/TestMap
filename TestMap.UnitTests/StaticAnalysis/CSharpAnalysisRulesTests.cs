using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using TestMap.Services.StaticAnalysis;

namespace TestMap.UnitTests.StaticAnalysis;

public sealed class CSharpAnalysisRulesTests
{
    /// <summary>
    /// Verifies that source document filtering includes normal C# source files and excludes generated or non-source files.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(@"C:\repo\src\OrderService.cs", true)]
    [InlineData(@"C:\repo\src\OrderService.CS", true)]
    [InlineData(@"C:\repo\src\OrderService.g.cs", false)]
    [InlineData(@"C:\repo\obj\Debug\net10.0\AssemblyInfo.cs", false)]
    [InlineData(@"C:\repo\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs", false)]
    [InlineData(@"C:\repo\src\OrderService.txt", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ShouldAnalyzeDocument_WithSourcePath_ReturnsExpectedDecision(string? filePath, bool expected)
    {
        // Act
        var shouldAnalyze = CSharpAnalysisRules.ShouldAnalyzeDocument(filePath);

        // Assert
        Assert.Equal(expected, shouldAnalyze);
    }

    /// <summary>
    /// Verifies that common assertion method names are recognized without requiring Roslyn symbol resolution.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Equal")]
    [InlineData("ThrowsAsync")]
    [InlineData("ShouldBe")]
    [InlineData("BeEquivalentTo")]
    [InlineData("ContainSingle")]
    public void IsAssertionInvocation_WithKnownAssertionMethodName_ReturnsTrue(string methodName)
    {
        // Act
        var isAssertion = CSharpAnalysisRules.IsAssertionInvocation(methodName);

        // Assert
        Assert.True(isAssertion);
    }

    /// <summary>
    /// Verifies that assertion-like containing types and namespaces are recognized even for unfamiliar method names.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Verify", "CustomAssertions", "", true)]
    [InlineData("Verify", "DomainAssert", "", true)]
    [InlineData("AnyMethod", "", "FluentAssertions.Execution", true)]
    [InlineData("AnyMethod", "", "Shouldly", true)]
    [InlineData("Calculate", "Calculator", "Sample.Domain", false)]
    public void IsAssertionInvocation_WithContainingMetadata_ReturnsExpectedDecision(
        string methodName,
        string containingTypeName,
        string containingNamespace,
        bool expected)
    {
        // Act
        var isAssertion = CSharpAnalysisRules.IsAssertionInvocation(
            methodName,
            containingTypeName,
            containingNamespace);

        // Assert
        Assert.Equal(expected, isAssertion);
    }

    /// <summary>
    /// Verifies that unresolved invocation text still detects explicit Assert calls.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void IsAssertionInvocation_WithAssertInvocationText_ReturnsTrue()
    {
        // Act
        var isAssertion = CSharpAnalysisRules.IsAssertionInvocation(
            methodName: "Custom",
            invocationText: "Assert.Custom(actual);");

        // Assert
        Assert.True(isAssertion);
    }

    /// <summary>
    /// Verifies that invocation method names are extracted from direct and member-access calls.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Assert.Equal(expected, actual)", "Equal")]
    [InlineData("ShouldBe(actual, expected)", "ShouldBe")]
    [InlineData("actual.Should().Be(expected)", "Be")]
    public void ExtractInvocationMethodName_WithInvocationExpression_ReturnsTerminalMethodName(
        string invocationText,
        string expected)
    {
        // Arrange
        var invocation = ParseInvocation(invocationText);

        // Act
        var methodName = CSharpAnalysisRules.ExtractInvocationMethodName(invocation);

        // Assert
        Assert.Equal(expected, methodName);
    }

    /// <summary>
    /// Verifies that named type symbols are normalized to the expected object kind strings.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("class Sample { }", "Sample", "class")]
    [InlineData("struct Sample { }", "Sample", "struct")]
    [InlineData("interface ISample { }", "ISample", "interface")]
    [InlineData("enum Sample { One }", "Sample", "enum")]
    [InlineData("delegate void Sample();", "Sample", "delegate")]
    [InlineData("record Sample(int Value);", "Sample", "record")]
    [InlineData("record struct Sample(int Value);", "Sample", "record_struct")]
    public void GetObjectKind_WithNamedTypeSymbol_ReturnsExpectedKind(
        string source,
        string typeName,
        string expected)
    {
        // Arrange
        var compilation = CreateCompilation(source);
        var symbol = compilation.GetTypeByMetadataName(typeName)
                     ?? throw new InvalidOperationException($"Could not resolve type: {typeName}");

        // Act
        var kind = CSharpAnalysisRules.GetObjectKind(symbol);

        // Assert
        Assert.Equal(expected, kind);
    }

    /// <summary>
    /// Verifies that member symbols are normalized to stable member kind strings.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("public Sample() { }", "Sample", "constructor")]
    [InlineData("public void Run() { }", "Run", "method")]
    [InlineData("public int Count { get; set; }", "Count", "property")]
    [InlineData("public int value;", "value", "field")]
    [InlineData("public event System.Action? Changed;", "Changed", "event")]
    public void GetMemberKind_WithMemberSymbol_ReturnsExpectedKind(
        string memberSource,
        string memberName,
        string expected)
    {
        // Arrange
        var symbol = ResolveMemberSymbol($$"""
            public class Sample
            {
                {{memberSource}}
            }
            """, "Sample", memberName);

        // Act
        var kind = CSharpAnalysisRules.GetMemberKind(symbol);

        // Assert
        Assert.Equal(expected, kind);
    }

    /// <summary>
    /// Verifies that relationship classification maps syntax and symbol pairs to relationship names.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("target.Run()", "Run", "calls")]
    [InlineData("new Target()", ".ctor", "creates")]
    [InlineData("target.Value", "Value", "references")]
    [InlineData("target.Changed", "Changed", "references")]
    public void GetMemberRelationshipType_WithReferencedSymbol_ReturnsExpectedRelationship(
        string expression,
        string expectedSymbolName,
        string expectedRelationship)
    {
        // Arrange
        var source = $$"""
            public class Target
            {
                public int Value;
                public event System.Action? Changed;
                public void Run() { }
            }

            public class Caller
            {
                public void Call(Target target)
                {
                    _ = {{expression}};
                }
            }
            """;
        var compilation = CreateCompilation(source);
        var expressionNode = ResolveExpression(compilation, expression);
        var resolvedSymbol = ResolveReferencedSymbol(compilation, expressionNode, expectedSymbolName);

        // Act
        var relationship = CSharpAnalysisRules.GetMemberRelationshipType(expressionNode, resolvedSymbol);

        // Assert
        Assert.Equal(expectedRelationship, relationship);
    }

    private static InvocationExpressionSyntax ParseInvocation(string text)
    {
        return SyntaxFactory.ParseExpression(text) as InvocationExpressionSyntax
               ?? throw new InvalidOperationException($"Could not parse invocation expression: {text}");
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Action).Assembly.Location)
            ]);
    }

    private static ISymbol ResolveMemberSymbol(string source, string typeName, string memberName)
    {
        var typeSymbol = CreateCompilation(source).GetTypeByMetadataName(typeName)
                         ?? throw new InvalidOperationException($"Could not resolve type: {typeName}");

        return memberName == typeName
            ? typeSymbol.Constructors.Single(x => !x.IsStatic)
            : typeSymbol.GetMembers(memberName).Single();
    }

    private static ExpressionSyntax ResolveExpression(CSharpCompilation compilation, string expression)
    {
        var root = compilation.SyntaxTrees.Single().GetRoot();

        return root.DescendantNodes()
                   .OfType<ExpressionSyntax>()
                   .First(node => node.ToString() == expression);
    }

    private static ISymbol ResolveReferencedSymbol(
        CSharpCompilation compilation,
        SyntaxNode node,
        string expectedSymbolName)
    {
        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());
        var symbol = semanticModel.GetSymbolInfo(node).Symbol
                     ?? semanticModel.GetSymbolInfo(node).CandidateSymbols.FirstOrDefault()
                     ?? throw new InvalidOperationException($"Could not resolve symbol for node: {node}");

        Assert.Equal(expectedSymbolName, symbol.Name);
        return symbol;
    }
}
