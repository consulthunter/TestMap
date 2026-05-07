using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestMap.Services.StaticAnalysis;

internal static class CSharpAnalysisRules
{
    public static bool ShouldAnalyzeDocument(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) ||
            !filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;

        return !filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
               && !filePath.EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)
               && !filePath.EndsWith("AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAssertionInvocation(InvocationExpressionSyntax invocation, ISymbol? symbol)
    {
        var methodSymbol = symbol as IMethodSymbol;
        var containingTypeName = methodSymbol?.ContainingType?.Name ?? string.Empty;
        var containingNamespace = methodSymbol?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        var methodName = methodSymbol?.Name ?? ExtractInvocationMethodName(invocation);

        return IsAssertionInvocation(
            methodName,
            containingTypeName,
            containingNamespace,
            invocation.ToFullString());
    }

    public static bool IsAssertionInvocation(
        string methodName,
        string containingTypeName = "",
        string containingNamespace = "",
        string invocationText = "")
    {
        if (containingTypeName.Contains("Assert", StringComparison.OrdinalIgnoreCase) ||
            containingTypeName.Contains("Assertion", StringComparison.OrdinalIgnoreCase))
            return true;

        if (containingNamespace.Contains("FluentAssertions", StringComparison.OrdinalIgnoreCase) ||
            containingNamespace.Contains("Shouldly", StringComparison.OrdinalIgnoreCase))
            return true;

        return methodName switch
        {
            "True" or
                "False" or
                "Equal" or
                "NotEqual" or
                "Same" or
                "NotSame" or
                "Null" or
                "NotNull" or
                "Empty" or
                "NotEmpty" or
                "Contains" or
                "DoesNotContain" or
                "StartsWith" or
                "EndsWith" or
                "Matches" or
                "Throws" or
                "ThrowsAsync" or
                "Throw" or
                "ThrowAsync" or
                "Fail" or
                "That" or
                "ShouldBe" or
                "ShouldNotBe" or
                "ShouldContain" or
                "ShouldNotContain" or
                "Be" or
                "BeTrue" or
                "BeFalse" or
                "BeNull" or
                "NotBeNull" or
                "BeEquivalentTo" or
                "ContainSingle" => true,
            _ => invocationText.Contains("Assert.", StringComparison.Ordinal)
        };
    }

    public static string ExtractInvocationMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            MemberAccessExpressionSyntax memberAccessExpression => memberAccessExpression.Name.Identifier.Text,
            _ => string.Empty
        };
    }

    public static string? GetMemberRelationshipType(SyntaxNode node, ISymbol symbol)
    {
        return node switch
        {
            InvocationExpressionSyntax when symbol is IMethodSymbol => "calls",
            ObjectCreationExpressionSyntax when symbol is IMethodSymbol => "creates",
            IdentifierNameSyntax when symbol is IFieldSymbol or IPropertySymbol or IEventSymbol => "references",
            MemberAccessExpressionSyntax when symbol is IFieldSymbol or IPropertySymbol or IEventSymbol => "references",
            _ => null
        };
    }

    public static string GetObjectKind(INamedTypeSymbol symbol)
    {
        if (symbol.IsRecord) return symbol.TypeKind == TypeKind.Struct ? "record_struct" : "record";

        return symbol.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => symbol.TypeKind.ToString().ToLowerInvariant()
        };
    }

    public static string GetMemberKind(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol methodSymbol => methodSymbol.MethodKind switch
            {
                MethodKind.Constructor => "constructor",
                MethodKind.StaticConstructor => "static_constructor",
                MethodKind.Destructor => "destructor",
                MethodKind.PropertyGet => "property_getter",
                MethodKind.PropertySet => "property_setter",
                MethodKind.EventAdd => "event_adder",
                MethodKind.EventRemove => "event_remover",
                MethodKind.UserDefinedOperator => "operator",
                MethodKind.Conversion => "conversion_operator",
                _ => "method"
            },
            IPropertySymbol => "property",
            IFieldSymbol => "field",
            IEventSymbol => "event",
            _ => symbol.Kind.ToString().ToLowerInvariant()
        };
    }
}
