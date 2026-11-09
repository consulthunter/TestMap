using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestMap.Services.TestGeneration.TargetSelection;

/// <summary>
/// Resolves a persisted member's accessibility for test-access decisions.
/// </summary>
public static class MemberVisibilityResolver
{
    /// <summary>
    /// Resolves visibility from the persisted modifiers, falling back to the modifiers on the
    /// parsed declaration header. The full member text is never searched: it can include the
    /// body and doc comments, where "public " or "private " say nothing about this member.
    /// Interface members without an access modifier are implicitly public.
    /// </summary>
    /// <param name="modifiers">Persisted modifier keywords of the member.</param>
    /// <param name="fullString">Persisted declaration text, including trivia and body.</param>
    /// <param name="objectKind">Kind of the containing object (class, interface, ...).</param>
    public static MemberVisibility Resolve(
        IReadOnlyCollection<string> modifiers,
        string fullString,
        string objectKind)
    {
        var fromModifiers = FromAccessKeywords(modifiers);
        if (fromModifiers != MemberVisibility.Unknown) return fromModifiers;

        var declaration = string.IsNullOrWhiteSpace(fullString)
            ? null
            : SyntaxFactory.ParseMemberDeclaration(fullString);
        if (declaration != null)
        {
            var fromHeader = FromAccessKeywords(declaration.Modifiers.Select(x => x.ValueText).ToList());
            if (fromHeader != MemberVisibility.Unknown) return fromHeader;
            if (HasExplicitInterfaceSpecifier(declaration)) return MemberVisibility.ExplicitInterface;
        }

        return string.Equals(objectKind, "interface", StringComparison.OrdinalIgnoreCase)
            ? MemberVisibility.Public
            : MemberVisibility.Unknown;
    }

    private static MemberVisibility FromAccessKeywords(IReadOnlyCollection<string> keywords)
    {
        bool Has(string keyword) => keywords.Any(x => x.Equals(keyword, StringComparison.OrdinalIgnoreCase));

        if (Has("public")) return MemberVisibility.Public;
        if (Has("private")) return MemberVisibility.Private;
        if (Has("protected")) return MemberVisibility.Protected;
        if (Has("internal")) return MemberVisibility.Internal;
        return MemberVisibility.Unknown;
    }

    private static bool HasExplicitInterfaceSpecifier(MemberDeclarationSyntax declaration)
    {
        return declaration switch
        {
            MethodDeclarationSyntax method => method.ExplicitInterfaceSpecifier != null,
            BasePropertyDeclarationSyntax property => property.ExplicitInterfaceSpecifier != null,
            _ => false
        };
    }
}
