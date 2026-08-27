using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef;
using CodeLocation = TestMap.Models.Code.Location;

namespace TestMap.Services.StaticAnalysis;

public sealed class RoslynMemberSymbolIndex
{
    private readonly Dictionary<int, RoslynMemberSymbolRow> _byId;
    private readonly Dictionary<string, List<RoslynMemberSymbolRow>> _byPathAndLine;
    private readonly Dictionary<string, List<RoslynMemberSymbolRow>> _byQualifiedName;
    private readonly Dictionary<string, IReadOnlyList<ISymbol>> _dispatchCache =
        new(StringComparer.Ordinal);

    public RoslynMemberSymbolIndex(IReadOnlyCollection<RoslynMemberSymbolRow> rows)
    {
        _byId = rows.ToDictionary(row => row.MemberId);
        _byPathAndLine = rows
            .GroupBy(
                row => $"{RoslynAnalysisUtilities.NormalizePath(row.FilePath)}:{row.Location.StartLineNumber}",
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        _byQualifiedName = rows
            .GroupBy(
                row => $"{row.Namespace}.{row.ObjectName}.{row.Name}",
                StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
    }

    public IReadOnlyCollection<RoslynMemberSymbolRow> Rows => _byId.Values;
    public RoslynMemberSymbolRow this[int id] => _byId[id];
    public bool TryGet(int id, out RoslynMemberSymbolRow row) => _byId.TryGetValue(id, out row!);

    public static Task<List<RoslynMemberSymbolRow>> LoadRowsAsync(
        TestMapDbContext dbContext,
        int solutionId,
        CancellationToken cancellationToken = default)
    {
        return (
                from member in dbContext.Members.AsNoTracking()
                join sourceObject in dbContext.Objects.AsNoTracking()
                    on member.ObjectEntityId equals sourceObject.Id
                join sourceFile in dbContext.Files.AsNoTracking()
                    on sourceObject.FileId equals sourceFile.Id
                join sourceProject in dbContext.CSharpProjects.AsNoTracking()
                    on sourceFile.CSharpProjectId equals sourceProject.Id
                where sourceProject.SolutionId == solutionId
                select new RoslynMemberSymbolRow(
                    member.Id,
                    member.Name,
                    member.Kind,
                    member.FullString,
                    member.Modifiers,
                    member.IsTestMember,
                    member.IsGenerated,
                    member.ContentHash,
                    sourceObject.Name,
                    sourceObject.Namespace,
                    sourceObject.IsTestObject,
                    sourceFile.FilePath,
                    sourceProject.FilePath,
                    sourceProject.BuildMetadata.IsTestProject,
                    member.Location))
            .ToListAsync(cancellationToken);
    }

    public RoslynMemberSymbolRow? TryResolve(ISymbol symbol)
    {
        symbol = RoslynAnalysisUtilities.NormalizeSymbol(symbol) ?? symbol;
        var location = symbol.Locations.FirstOrDefault(candidate =>
            candidate.IsInSource && candidate.SourceTree?.FilePath != null);
        if (location != null)
        {
            var span = location.GetLineSpan();
            var key =
                $"{RoslynAnalysisUtilities.NormalizePath(location.SourceTree!.FilePath)}:{span.StartLinePosition.Line}";
            if (_byPathAndLine.TryGetValue(key, out var lineCandidates))
            {
                var exact = lineCandidates
                    .Where(candidate => Matches(candidate, symbol))
                    .OrderBy(candidate => OverloadDistance(candidate, symbol))
                    .ThenByDescending(candidate => candidate.MemberId)
                    .FirstOrDefault();
                if (exact != null) return exact;
            }
        }

        var containingType = symbol.ContainingType;
        var qualifiedName = containingType == null
            ? string.Empty
            : $"{containingType.ContainingNamespace}.{containingType.Name}.{RoslynAnalysisUtilities.ResolveMemberName(symbol)}";
        // A member is identified partly by its body, so a method that has been edited leaves an
        // earlier version behind under the same qualified name. Prefer the newest.
        return _byQualifiedName.TryGetValue(qualifiedName, out var candidates)
            ? candidates
                .Where(candidate => Matches(candidate, symbol))
                .OrderBy(candidate => OverloadDistance(candidate, symbol))
                .ThenByDescending(candidate => candidate.MemberId)
                .FirstOrDefault()
            : null;
    }

    public IReadOnlyList<ISymbol> ResolveCandidateTargets(
        ISymbol symbol,
        Compilation compilation)
    {
        symbol = RoslynAnalysisUtilities.NormalizeSymbol(symbol) ?? symbol;
        if (symbol is not IMethodSymbol method)
            return [symbol];

        var cacheKey = method.GetDocumentationCommentId() ??
                       method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (_dispatchCache.TryGetValue(cacheKey, out var cached)) return cached;

        var result = new List<ISymbol> { method };
        if (RequiresDispatchResolution(method))
        {
            foreach (var type in EnumerateNamedTypes(compilation.GlobalNamespace))
            foreach (var implementation in GetImplementationCandidates(type, method))
            {
                var normalized = RoslynAnalysisUtilities.NormalizeSymbol(implementation);
                if (normalized != null &&
                    result.All(existing =>
                        !SymbolEqualityComparer.Default.Equals(existing, normalized)))
                    result.Add(normalized);
            }
        }

        var resolved = result
            .Where(candidate => TryResolve(candidate) != null)
            .ToList();
        _dispatchCache[cacheKey] = resolved;
        return resolved;
    }

    public static bool RequiresDispatchResolution(IMethodSymbol method)
    {
        return method.ContainingType.TypeKind == TypeKind.Interface ||
               method.IsAbstract ||
               (method.IsVirtual && !method.IsSealed);
    }

    private static bool Matches(RoslynMemberSymbolRow candidate, ISymbol symbol)
    {
        return candidate.Kind == RoslynAnalysisUtilities.ResolveMemberKind(symbol) &&
               (candidate.Name == RoslynAnalysisUtilities.ResolveMemberName(symbol) ||
                symbol is IMethodSymbol { MethodKind: MethodKind.Constructor });
    }

    private static int OverloadDistance(RoslynMemberSymbolRow candidate, ISymbol symbol)
    {
        if (symbol is not IMethodSymbol method) return 0;
        var parsed = SyntaxFactory.ParseMemberDeclaration(candidate.FullString);
        var parameterCount = parsed switch
        {
            MethodDeclarationSyntax declaration => declaration.ParameterList.Parameters.Count,
            ConstructorDeclarationSyntax declaration => declaration.ParameterList.Parameters.Count,
            _ => -1
        };
        return parameterCount < 0 ? int.MaxValue : Math.Abs(parameterCount - method.Parameters.Length);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        foreach (var nested in EnumerateNamedTypes(type))
            yield return nested;

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        foreach (var type in EnumerateNamedTypes(childNamespace))
            yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers())
        foreach (var child in EnumerateNamedTypes(nested))
            yield return child;
    }

    private static IEnumerable<IMethodSymbol> GetImplementationCandidates(
        INamedTypeSymbol type,
        IMethodSymbol target)
    {
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)) yield break;

        if (target.ContainingType.TypeKind == TypeKind.Interface)
        {
            foreach (var matchingInterface in type.AllInterfaces.Where(interfaceType =>
                         SymbolEqualityComparer.Default.Equals(
                             interfaceType.OriginalDefinition,
                             target.ContainingType.OriginalDefinition)))
            foreach (var interfaceMember in matchingInterface.GetMembers(target.Name).OfType<IMethodSymbol>())
            {
                var implementation = type.FindImplementationForInterfaceMember(interfaceMember);
                if (implementation is IMethodSymbol implementationMethod)
                    yield return implementationMethod;
            }
        }

        foreach (var member in type.GetMembers().OfType<IMethodSymbol>())
        for (var current = member.OverriddenMethod; current != null; current = current.OverriddenMethod)
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target))
                yield return member;
    }
}

public sealed record RoslynMemberSymbolRow(
    int MemberId,
    string Name,
    string Kind,
    string FullString,
    IReadOnlyList<string> Modifiers,
    bool IsTestMember,
    bool IsGenerated,
    string ContentHash,
    string ObjectName,
    string Namespace,
    bool IsTestObject,
    string FilePath,
    string ProjectPath,
    bool IsTestProject,
    CodeLocation Location)
{
    public bool IsProduction =>
        Kind is "method" or "property" or "field" or "constructor" &&
        !IsTestMember &&
        !IsGenerated &&
        !IsTestObject &&
        !IsTestProject &&
        !RoslynAnalysisUtilities.IsLikelyTestPath(FilePath) &&
        !RoslynAnalysisUtilities.IsLikelyTestPath(ProjectPath);

    public bool IsTestSupport =>
        IsTestObject ||
        IsTestProject ||
        RoslynAnalysisUtilities.IsLikelyTestPath(FilePath) ||
        RoslynAnalysisUtilities.IsLikelyTestPath(ProjectPath);
}
