using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Services.StaticAnalysis.Assertions;

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
        return AssertionPatternCatalog.Shared.Match(invocation, symbol) != null;
    }

    public static bool IsAssertionInvocation(
        string methodName,
        string containingTypeName = "",
        string containingNamespace = "",
        string invocationText = "")
    {
        return AssertionPatternCatalog.Shared.MatchLegacy(
            methodName,
            containingTypeName,
            containingNamespace,
            invocationText) != null;
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
