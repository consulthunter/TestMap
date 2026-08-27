using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using TestMap.Models.Experiment.Assertions;

namespace TestMap.Services.StaticAnalysis.Assertions;

public enum AssertionOperandShape
{
    Unknown,
    StaticArguments,
    NUnitConstraint,
    FluentReceiver,
    ShouldlyReceiver,
    ExceptionDelegate
}

public sealed record AssertionPatternMatch(
    string Framework,
    string Style,
    string MethodName,
    string MethodDisplay,
    AssertionRecognitionKind RecognitionKind,
    AssertionOperandShape OperandShape,
    bool IsSupported);

public sealed class AssertionPatternCatalog
{
    public const string Version = AssertionLineagePolicy.CurrentCatalogVersion;

    private static readonly HashSet<string> StaticTerminals =
    [
        "True", "False", "IsTrue", "IsFalse",
        "Equal", "NotEqual", "AreEqual", "AreNotEqual",
        "Same", "NotSame", "AreSame", "AreNotSame",
        "Null", "NotNull", "IsNull", "IsNotNull",
        "Empty", "NotEmpty", "IsEmpty", "IsNotEmpty",
        "Contains", "DoesNotContain", "StartsWith", "EndsWith", "Matches",
        "Single", "All", "Collection", "Fail", "Pass", "Inconclusive"
    ];

    private static readonly HashSet<string> ExceptionTerminals =
    [
        "Throws", "ThrowsAsync", "ThrowsAny", "ThrowsAnyAsync",
        "DoesNotThrow", "DoesNotThrowAsync", "Throw", "ThrowAsync",
        "NotThrow", "NotThrowAsync"
    ];

    private static readonly HashSet<string> FluentTerminals =
    [
        "Be", "NotBe", "BeTrue", "BeFalse", "BeNull", "NotBeNull",
        "BeEquivalentTo", "NotBeEquivalentTo", "Contain", "NotContain",
        "ContainSingle", "BeEmpty", "NotBeEmpty", "Match", "StartWith",
        "EndWith", "Throw", "ThrowAsync", "NotThrow", "NotThrowAsync"
    ];

    private static readonly HashSet<string> LegacyTerminals =
        new(StaticTerminals.Concat(ExceptionTerminals).Concat(FluentTerminals)
            .Concat(["That", "ShouldBe", "ShouldNotBe", "ShouldContain", "ShouldNotContain"]),
            StringComparer.Ordinal);

    public static AssertionPatternCatalog Shared { get; } = new();

    public AssertionPatternMatch? Match(
        InvocationExpressionSyntax invocation,
        ISymbol? symbol)
    {
        var method = symbol as IMethodSymbol;
        var methodName = method?.Name ?? CSharpAnalysisRules.ExtractInvocationMethodName(invocation);
        var containingType = method?.ContainingType?.Name ?? string.Empty;
        var containingNamespace = method?.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        return MatchCore(
            methodName,
            containingType,
            containingNamespace,
            invocation.ToFullString(),
            method);
    }

    public AssertionPatternMatch? Match(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        return MatchCore(
            method.Name,
            method.ContainingType?.Name ?? string.Empty,
            method.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            invocation.Syntax.ToFullString(),
            method);
    }

    public AssertionPatternMatch? MatchLegacy(
        string methodName,
        string containingTypeName = "",
        string containingNamespace = "",
        string invocationText = "")
    {
        return MatchCore(
            methodName,
            containingTypeName,
            containingNamespace,
            invocationText,
            null);
    }

    private static AssertionPatternMatch? MatchCore(
        string methodName,
        string containingTypeName,
        string containingNamespace,
        string invocationText,
        IMethodSymbol? method)
    {
        var semantic = method != null;
        if (IsFluentAssertions(containingNamespace))
        {
            if (methodName == "Should") return null;
            var supported = FluentTerminals.Contains(methodName);
            return Create(
                "FluentAssertions",
                "Fluent",
                methodName,
                method,
                semantic,
                supported && ExceptionTerminals.Contains(methodName)
                    ? AssertionOperandShape.ExceptionDelegate
                    : AssertionOperandShape.FluentReceiver,
                supported);
        }

        if (IsShouldly(containingNamespace))
        {
            var supported = methodName.StartsWith("Should", StringComparison.Ordinal);
            return Create(
                "Shouldly",
                "Extension",
                methodName,
                method,
                semantic,
                AssertionOperandShape.ShouldlyReceiver,
                supported);
        }

        if (IsXunit(containingNamespace, containingTypeName))
            return MatchClassic(
                "xUnit",
                methodName,
                method,
                semantic,
                allowThat: false);

        if (IsNUnit(containingNamespace, containingTypeName))
        {
            if (methodName == "That")
                return Create(
                    "NUnit",
                    "Constraint",
                    methodName,
                    method,
                    semantic,
                    AssertionOperandShape.NUnitConstraint,
                    true);
            return MatchClassic(
                "NUnit",
                methodName,
                method,
                semantic,
                allowThat: true);
        }

        if (IsMSTest(containingNamespace, containingTypeName))
            return MatchClassic(
                "MSTest",
                methodName,
                method,
                semantic,
                allowThat: false);

        // No known assertion framework matched. Recognition here must key off the call site
        // being an assertion, never off the method name alone: names such as Contains, Single,
        // All and Empty collide with ordinary operand calls (string.Contains, list.Single),
        // and matching those counts an assertion's own operands as extra assertions.
        var declaringTypeLooksAssertion =
            containingTypeName.Contains("Assert", StringComparison.OrdinalIgnoreCase) ||
            containingTypeName.Contains("Assertion", StringComparison.OrdinalIgnoreCase);
        if (!declaringTypeLooksAssertion)
        {
            // A resolved symbol that is not declared on an assertion type is an ordinary call.
            if (method != null) return null;

            // A bare name query carries no call-site context to judge, so the terminal-name
            // heuristic is the only signal available. Analysis paths always supply the
            // invocation text and therefore never take this branch.
            var nameOnlyQuery = invocationText.Length == 0 && containingNamespace.Length == 0;
            if (!nameOnlyQuery &&
                !HasAssertionReceiver(invocationText) &&
                !methodName.StartsWith("Should", StringComparison.Ordinal))
                return null;

            if (nameOnlyQuery && !LegacyTerminals.Contains(methodName)) return null;
        }

        return Create(
            "LegacyOrCustom",
            "Fallback",
            methodName,
            method,
            semantic: false,
            AssertionOperandShape.Unknown,
            supported: false);
    }

    private static AssertionPatternMatch? MatchClassic(
        string framework,
        string methodName,
        IMethodSymbol? method,
        bool semantic,
        bool allowThat)
    {
        if (allowThat && methodName == "That")
            return Create(
                framework,
                "Constraint",
                methodName,
                method,
                semantic,
                AssertionOperandShape.NUnitConstraint,
                true);

        if (ExceptionTerminals.Contains(methodName))
            return Create(
                framework,
                "Exception",
                methodName,
                method,
                semantic,
                AssertionOperandShape.ExceptionDelegate,
                true);

        var supported = StaticTerminals.Contains(methodName);
        return Create(
            framework,
            "Classic",
            methodName,
            method,
            semantic,
            AssertionOperandShape.StaticArguments,
            supported);
    }

    private static AssertionPatternMatch Create(
        string framework,
        string style,
        string methodName,
        IMethodSymbol? method,
        bool semantic,
        AssertionOperandShape shape,
        bool supported)
    {
        return new AssertionPatternMatch(
            framework,
            style,
            methodName,
            method?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? methodName,
            semantic ? AssertionRecognitionKind.Semantic : AssertionRecognitionKind.SyntacticFallback,
            shape,
            supported);
    }

    /// <summary>
    /// Determines whether the qualifier written before the invoked method looks like an
    /// assertion entry point (Assert, CollectionAssert, Xunit.Assert, ...). Used only when the
    /// semantic model could not resolve the symbol.
    /// </summary>
    private static bool HasAssertionReceiver(string invocationText)
    {
        if (string.IsNullOrWhiteSpace(invocationText)) return false;

        var callee = invocationText.AsSpan();
        var openParen = callee.IndexOf('(');
        if (openParen >= 0) callee = callee[..openParen];

        var lastDot = callee.LastIndexOf('.');
        if (lastDot < 0) return false;

        return callee[..lastDot].Contains("Assert", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsXunit(string ns, string type) =>
        ns.StartsWith("Xunit", StringComparison.Ordinal) &&
        type.Contains("Assert", StringComparison.OrdinalIgnoreCase);

    private static bool IsNUnit(string ns, string type) =>
        ns.StartsWith("NUnit.Framework", StringComparison.Ordinal) &&
        type.Contains("Assert", StringComparison.OrdinalIgnoreCase);

    private static bool IsMSTest(string ns, string type) =>
        ns.StartsWith("Microsoft.VisualStudio.TestTools.UnitTesting", StringComparison.Ordinal) &&
        type.Contains("Assert", StringComparison.OrdinalIgnoreCase);

    private static bool IsFluentAssertions(string ns) =>
        ns.StartsWith("FluentAssertions", StringComparison.Ordinal);

    private static bool IsShouldly(string ns) =>
        ns.StartsWith("Shouldly", StringComparison.Ordinal);
}
