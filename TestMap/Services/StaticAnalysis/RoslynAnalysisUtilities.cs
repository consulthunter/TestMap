using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestMap.Services.StaticAnalysis;

internal static class RoslynAnalysisUtilities
{
    public static ISymbol? NormalizeSymbol(ISymbol? symbol)
    {
        return symbol switch
        {
            IMethodSymbol { ReducedFrom: not null } reduced =>
                reduced.ReducedFrom.OriginalDefinition,
            IMethodSymbol method => method.OriginalDefinition,
            IPropertySymbol property => property.OriginalDefinition,
            IFieldSymbol field => field.OriginalDefinition,
            IEventSymbol @event => @event.OriginalDefinition,
            _ => null
        };
    }

    public static ISymbol? ResolveReferencedSymbol(SyntaxNode node, SemanticModel semanticModel)
    {
        var info = semanticModel.GetSymbolInfo(node);
        return NormalizeSymbol(info.Symbol ?? info.CandidateSymbols.FirstOrDefault());
    }

    public static Document? FindDocumentByPath(Solution solution, string filePath)
    {
        var normalized = NormalizePath(filePath);
        return solution.Projects
            .SelectMany(project => project.Documents)
            .FirstOrDefault(document =>
                document.FilePath != null &&
                string.Equals(
                    NormalizePath(document.FilePath),
                    normalized,
                    StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Locates the declaration a persisted member row describes. Matching is constrained to the
    /// type the row belongs to: a member name is unique only within its declaring type, and a
    /// generated test file routinely declares several helper classes that each carry a member of
    /// the same name (ReadLine, Dispose, a constructor). Searching the file by name alone made
    /// those collide, and because a row's recorded line goes stale as soon as anything is
    /// inserted above it, the exact-line check missed and the first same-named declaration in
    /// the file won. When the line no longer matches, the nearest declaration is the best
    /// available evidence, so prefer that over document order.
    /// </summary>
    public static MemberDeclarationSyntax? FindMemberDeclaration(
        SyntaxNode root,
        RoslynMemberSymbolRow member)
    {
        var candidates = root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Where(node => node is MethodDeclarationSyntax
                or ConstructorDeclarationSyntax
                or PropertyDeclarationSyntax)
            .Where(node => DeclaringTypeMatches(node, member))
            .Where(node => node switch
            {
                MethodDeclarationSyntax method => method.Identifier.Text == member.Name,
                ConstructorDeclarationSyntax constructor =>
                    constructor.Identifier.Text == member.ObjectName ||
                    member.Kind == "constructor",
                PropertyDeclarationSyntax property => property.Identifier.Text == member.Name,
                _ => false
            })
            .ToList();

        return candidates
            .OrderBy(candidate => Math.Abs(
                candidate.GetLocation().GetLineSpan().StartLinePosition.Line -
                member.Location.StartLineNumber))
            .FirstOrDefault();
    }

    /// <summary>
    /// Whether the declaration sits inside the type the member row names. A row with no recorded
    /// type name carries no constraint to apply, so it matches anything.
    /// </summary>
    private static bool DeclaringTypeMatches(SyntaxNode node, RoslynMemberSymbolRow member)
    {
        if (string.IsNullOrEmpty(member.ObjectName)) return true;

        var declaringType = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        return declaringType != null &&
               declaringType.Identifier.Text == member.ObjectName;
    }

    public static MemberDeclarationSyntax? TryGetTargetDeclaration(
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference == null) return null;

        return syntaxReference.GetSyntax(cancellationToken)
            .AncestorsAndSelf()
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault();
    }

    public static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static string ResolveMemberName(ISymbol symbol)
    {
        return symbol is IMethodSymbol { MethodKind: MethodKind.Constructor }
            ? symbol.ContainingType.Name
            : symbol.Name;
    }

    public static string ResolveMemberKind(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol { MethodKind: MethodKind.Constructor } => "constructor",
            IMethodSymbol => "method",
            IPropertySymbol => "property",
            IFieldSymbol => "field",
            IEventSymbol => "event",
            _ => symbol.Kind.ToString().ToLowerInvariant()
        };
    }

    public static bool IsLikelyTestPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("test/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/test/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains(".tests/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains(".test/", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".Tests.csproj", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".Test.csproj", StringComparison.OrdinalIgnoreCase);
    }
}
