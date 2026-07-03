using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;
using CodeLocation = TestMap.Models.Code.Location;

namespace TestMap.Services.StaticAnalysis;

public sealed class RoslynSourceTestTraceService : IRoslynSourceTestTraceService
{
    private const string ResolverVersion = "roslyn-source-test-trace-v1";

    private readonly ProjectContext _context;
    private readonly TestMapDbContext _dbContext;
    private readonly IStaticAnalysisWorkspace _workspace;

    public RoslynSourceTestTraceService(
        ProjectContext context,
        TestMapDbContext dbContext,
        IStaticAnalysisWorkspace workspace)
    {
        _context = context;
        _dbContext = dbContext;
        _workspace = workspace;
    }

    public async Task<List<SourceTestMappingEntity>?> TraceAsync(
        int projectId,
        int solutionId,
        CancellationToken cancellationToken = default)
    {
        var solutionEntity = await _dbContext.CSharpSolutions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == solutionId, cancellationToken);
        if (solutionEntity == null || string.IsNullOrWhiteSpace(solutionEntity.FilePath)) return null;

        var memberRows = await LoadMemberRowsAsync(solutionId, cancellationToken);
        if (memberRows.Count == 0) return [];

        var index = new MemberSymbolIndex(memberRows);
        var solution = await _workspace.OpenSolutionAsync(solutionEntity.FilePath, cancellationToken);
        var testMembers = memberRows
            .Where(x => x.IsTestMember && x.Kind == "method")
            .OrderBy(x => x.MemberId)
            .ToList();

        var config = _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection;
        var mappings = new List<SourceTestMappingEntity>();
        foreach (var testMember in testMembers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = FindDocumentByPath(solution, testMember.FilePath);
            if (document == null) continue;

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (root == null || semanticModel == null) continue;

            var declaration = FindMemberDeclaration(root, testMember);
            if (declaration == null) continue;

            foreach (var path in TraceDeclaration(
                         declaration,
                         semanticModel,
                         solution,
                         index,
                         new TracePath([testMember.MemberId], 0),
                         config.MaxTestCodeDepth,
                         cancellationToken))
            {
                mappings.Add(CreateMapping(projectId, testMember.MemberId, path, index));
            }
        }

        return mappings
            .GroupBy(x => new { x.SourceMemberId, x.TestMemberId, x.EvidenceKind })
            .Select(x => x.OrderBy(mapping => mapping.PathLength).ThenByDescending(mapping => mapping.Confidence).First())
            .ToList();
    }

    private static IEnumerable<IReadOnlyList<int>> TraceDeclaration(
        MemberDeclarationSyntax declaration,
        SemanticModel semanticModel,
        Solution solution,
        MemberSymbolIndex index,
        TracePath path,
        int maxTestDepth,
        CancellationToken cancellationToken)
    {
        foreach (var node in GetTraceNodes(declaration))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = ResolveReferencedSymbol(node, semanticModel);
            if (symbol == null || IsAssertionInvocation(node, symbol)) continue;

            foreach (var targetSymbol in ResolveCandidateTargets(symbol, semanticModel.Compilation, index))
            {
                var target = index.TryResolve(targetSymbol);
                if (target == null || path.MemberIds.Contains(target.MemberId)) continue;

                var nextPathIds = path.MemberIds.Concat([target.MemberId]).ToList();
                if (target.IsProduction)
                {
                    // Stop at the first production member — do not traverse into production code.
                    // Source-test mapping establishes intent (which production method this test
                    // covers), not internal implementation structure. What a production method
                    // calls internally is a separate concern handled at prompt-construction time.
                    yield return nextPathIds;
                }
                else if (target.IsTestSupport && path.TestDepth < maxTestDepth)
                {
                    var targetDeclaration = TryGetTargetDeclaration(targetSymbol, solution, cancellationToken);
                    if (targetDeclaration == null) continue;

                    var targetDocument = solution.GetDocument(targetDeclaration.SyntaxTree);
                    if (targetDocument == null) continue;

                    var targetSemanticModel = targetDocument.GetSemanticModelAsync(cancellationToken)
                        .GetAwaiter()
                        .GetResult();
                    if (targetSemanticModel == null) continue;

                    foreach (var helperPath in TraceDeclaration(
                                 targetDeclaration,
                                 targetSemanticModel,
                                 solution,
                                 index,
                                 new TracePath(nextPathIds, path.TestDepth + 1),
                                 maxTestDepth,
                                 cancellationToken))
                        yield return helperPath;
                }
            }
        }
    }

    private async Task<List<MemberSymbolRow>> LoadMemberRowsAsync(
        int solutionId,
        CancellationToken cancellationToken)
    {
        return await (
                from member in _dbContext.Members.AsNoTracking()
                join sourceObject in _dbContext.Objects.AsNoTracking() on member.ObjectEntityId equals sourceObject.Id
                join sourceFile in _dbContext.Files.AsNoTracking() on sourceObject.FileId equals sourceFile.Id
                join sourceProject in _dbContext.CSharpProjects.AsNoTracking() on sourceFile.CSharpProjectId equals sourceProject.Id
                where sourceProject.SolutionId == solutionId
                select new MemberSymbolRow(
                    member.Id,
                    member.Name,
                    member.Kind,
                    member.FullString,
                    member.Modifiers,
                    member.IsTestMember,
                    member.IsGenerated,
                    sourceObject.Name,
                    sourceObject.Namespace,
                    sourceObject.IsTestObject,
                    sourceFile.FilePath,
                    sourceProject.FilePath,
                    sourceProject.BuildMetadata.IsTestProject,
                    member.Location))
            .ToListAsync(cancellationToken);
    }

    private static IEnumerable<SyntaxNode> GetTraceNodes(MemberDeclarationSyntax declaration)
    {
        // Only real call sites: method invocations and object-creation expressions.
        // MemberAccessExpressionSyntax and IdentifierNameSyntax are intentionally excluded:
        // they duplicate the same symbol as the enclosing InvocationExpressionSyntax and
        // trigger GetSymbolInfo on every identifier (locals, type names, etc.), creating
        // O(tokens) redundant lookups with no additional mapping coverage.
        return declaration.DescendantNodes().Where(node =>
            node is InvocationExpressionSyntax
                or ObjectCreationExpressionSyntax);
    }

    private static SourceTestMappingEntity CreateMapping(
        int projectId,
        int testMemberId,
        IReadOnlyList<int> path,
        MemberSymbolIndex index)
    {
        var sourceMemberId = path[^1];
        var evidenceKind = ResolveEvidenceKind(path, index);
        return new SourceTestMappingEntity
        {
            ProjectId = projectId,
            SourceMemberId = sourceMemberId,
            TestMemberId = testMemberId,
            EvidenceKind = evidenceKind,
            // Depth 1–2 hops through test code (path.Count ≤ 3: [test, ?helper, target])
            // are strongly grounded. Deeper paths through multiple test helpers are weaker
            // evidence and are marked ungrounded so downstream consumers can weight accordingly.
            IsGrounded = path.Count <= 3,
            AccessPathStrategy = ResolveAccessPathStrategy(path, index),
            PathLength = path.Count - 1,
            Confidence = ResolveConfidence(evidenceKind),
            Summary = BuildSummary(path, index, evidenceKind),
            ResolverVersion = ResolverVersion,
            CreatedAt = DateTime.UtcNow,
            TraceSteps = BuildTraceSteps(path)
        };
    }

    private static MemberDeclarationSyntax? FindMemberDeclaration(SyntaxNode root, MemberSymbolRow member)
    {
        var candidates = root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Where(x => x is MethodDeclarationSyntax or ConstructorDeclarationSyntax or PropertyDeclarationSyntax)
            .Where(x => x switch
            {
                MethodDeclarationSyntax method => method.Identifier.Text == member.Name,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.Text == member.ObjectName ||
                                                            member.Kind == "constructor",
                PropertyDeclarationSyntax property => property.Identifier.Text == member.Name,
                _ => false
            })
            .ToList();

        return candidates.FirstOrDefault(x =>
            {
                var span = x.GetLocation().GetLineSpan();
                return span.StartLinePosition.Line == member.Location.StartLineNumber;
            }) ?? candidates.FirstOrDefault();
    }

    private static MemberDeclarationSyntax? TryGetTargetDeclaration(
        ISymbol symbol,
        Solution solution,
        CancellationToken cancellationToken)
    {
        var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference == null) return null;

        var syntax = syntaxReference.GetSyntax(cancellationToken);
        return syntax.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault();
    }

    private static ISymbol? ResolveReferencedSymbol(SyntaxNode node, SemanticModel semanticModel)
    {
        var info = semanticModel.GetSymbolInfo(node);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        return NormalizeSymbol(symbol);
    }

    private static ISymbol? NormalizeSymbol(ISymbol? symbol)
    {
        return symbol switch
        {
            IMethodSymbol { ReducedFrom: not null } reducedMethod => reducedMethod.ReducedFrom.OriginalDefinition,
            IMethodSymbol methodSymbol => methodSymbol.OriginalDefinition,
            IPropertySymbol propertySymbol => propertySymbol.OriginalDefinition,
            IFieldSymbol fieldSymbol => fieldSymbol.OriginalDefinition,
            IEventSymbol eventSymbol => eventSymbol.OriginalDefinition,
            _ => null
        };
    }

    private static IEnumerable<ISymbol> ResolveCandidateTargets(
        ISymbol symbol,
        Compilation compilation,
        MemberSymbolIndex index)
    {
        yield return symbol;

        if (symbol is not IMethodSymbol methodSymbol) yield break;

        var target = methodSymbol.OriginalDefinition;
        foreach (var type in EnumerateCompilationTypes(compilation))
        foreach (var implementation in GetImplementationCandidates(type, target))
        {
            var normalized = NormalizeSymbol(implementation);
            if (normalized != null && !SymbolEqualityComparer.Default.Equals(normalized, target) && index.TryResolve(normalized) != null)
                yield return normalized;
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateCompilationTypes(Compilation compilation)
    {
        // Only enumerate source types from the compilation's own global namespace.
        // BCL and NuGet types are never in the MemberSymbolIndex (the index only holds
        // members from DB-persisted solution projects), so walking referenced assemblies
        // is pure overhead: 50k+ type iterations for a typical .NET project, called
        // once per invocation node per test method.
        return EnumerateNamedTypes(compilation.GlobalNamespace);
    }

    private static IEnumerable<IMethodSymbol> GetImplementationCandidates(INamedTypeSymbol type, IMethodSymbol target)
    {
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)) yield break;

        if (target.ContainingType.TypeKind == TypeKind.Interface &&
            type.AllInterfaces.FirstOrDefault(interfaceType =>
                SymbolEqualityComparer.Default.Equals(interfaceType.OriginalDefinition, target.ContainingType.OriginalDefinition)) is { } matchingInterface)
        {
            foreach (var interfaceMember in matchingInterface.GetMembers(target.Name).OfType<IMethodSymbol>())
            {
                var implementation = type.FindImplementationForInterfaceMember(interfaceMember);
                if (implementation is IMethodSymbol implementationMethod) yield return implementationMethod;
            }
        }

        foreach (var member in type.GetMembers().OfType<IMethodSymbol>())
            for (var current = member.OverriddenMethod; current != null; current = current.OverriddenMethod)
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target))
                    yield return member;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        foreach (var nestedType in EnumerateNamedTypes(type))
            yield return nestedType;

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        foreach (var type in EnumerateNamedTypes(childNamespace))
            yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nestedType in type.GetTypeMembers())
        foreach (var child in EnumerateNamedTypes(nestedType))
            yield return child;
    }

    private static bool IsAssertionInvocation(SyntaxNode node, ISymbol symbol)
    {
        return node is InvocationExpressionSyntax invocation &&
               CSharpAnalysisRules.IsAssertionInvocation(invocation, symbol);
    }

    private static Document? FindDocumentByPath(Solution solution, string filePath)
    {
        var normalized = NormalizePath(filePath);
        return solution.Projects
            .SelectMany(x => x.Documents)
            .FirstOrDefault(x => x.FilePath != null &&
                                 string.Equals(NormalizePath(x.FilePath), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static List<SourceTestMappingTraceStepEntity> BuildTraceSteps(IReadOnlyList<int> path)
    {
        var steps = new List<SourceTestMappingTraceStepEntity>();
        for (var index = 0; index < path.Count - 1; index++)
            steps.Add(new SourceTestMappingTraceStepEntity
            {
                StepIndex = index,
                FromMemberId = path[index],
                ToMemberId = path[index + 1],
                RelationshipKind = "calls",
                EdgeSource = "Roslyn",
                Summary = $"Roslyn trace step {index}: member {path[index]} -> member {path[index + 1]}."
            });

        return steps;
    }

    private static string ResolveEvidenceKind(IReadOnlyList<int> path, MemberSymbolIndex index)
    {
        var target = index[path[^1]];
        if (path.Count == 2)
            return target.Kind == "constructor"
                ? "DirectConstructorInvocation"
                : "DirectMethodInvocation";

        if (path.Skip(1).SkipLast(1).Any(id => index[id].IsTestSupport))
            return "HelperMediatedPath";

        var firstProduction = path.Skip(1).FirstOrDefault(id => index[id].IsProduction);
        if (firstProduction != 0 && index[firstProduction].Kind == "constructor")
            return "ConstructorMediatedPath";

        var productionHopCount = Math.Max(0, path.Skip(1).Count(id => index[id].IsProduction) - 1);
        return productionHopCount <= 1
            ? "ProductionMethodPath"
            : "DeepProductionMethodPath";
    }

    private static string ResolveAccessPathStrategy(IReadOnlyList<int> path, MemberSymbolIndex index)
    {
        var evidenceKind = ResolveEvidenceKind(path, index);
        if (evidenceKind == "HelperMediatedPath") return TestAccessStrategy.HelperMediatedPath.ToString();

        var target = index[path[^1]];
        var visibility = ResolveVisibility(target);
        if (path.Count == 2)
            return visibility switch
            {
                MemberVisibility.Public => TestAccessStrategy.DirectPublicCall.ToString(),
                MemberVisibility.Internal => TestAccessStrategy.DirectInternalCall.ToString(),
                MemberVisibility.Protected => TestAccessStrategy.ProtectedSubclassHarness.ToString(),
                _ => TestAccessStrategy.NotReasonablyTestable.ToString()
            };

        return visibility switch
        {
            MemberVisibility.Public => TestAccessStrategy.PublicCallerPath.ToString(),
            MemberVisibility.Internal => TestAccessStrategy.InternalCallerPath.ToString(),
            _ => TestAccessStrategy.PublicCallerPath.ToString()
        };
    }

    private static double ResolveConfidence(string evidenceKind)
    {
        return evidenceKind switch
        {
            "DirectMethodInvocation" => 1.0,
            "DirectConstructorInvocation" => 0.65,
            "HelperMediatedPath" => 0.9,
            "ProductionMethodPath" => 0.85,
            "DeepProductionMethodPath" => 0.7,
            "ConstructorMediatedPath" => 0.55,
            _ => 0.75
        };
    }

    private static string BuildSummary(
        IReadOnlyList<int> path,
        MemberSymbolIndex index,
        string evidenceKind)
    {
        var names = path.Select(id => $"{index[id].Name}({id})");
        return $"{evidenceKind} Roslyn trace: {string.Join(" -> ", names)}.";
    }

    private static MemberVisibility ResolveVisibility(MemberSymbolRow member)
    {
        if (member.Modifiers.Any(x => x.Equals("public", StringComparison.OrdinalIgnoreCase)) ||
            member.FullString.Contains("public ", StringComparison.Ordinal))
            return MemberVisibility.Public;
        if (member.Modifiers.Any(x => x.Equals("private", StringComparison.OrdinalIgnoreCase)) ||
            member.FullString.Contains("private ", StringComparison.Ordinal))
            return MemberVisibility.Private;
        if (member.Modifiers.Any(x => x.Equals("protected", StringComparison.OrdinalIgnoreCase)) ||
            member.FullString.Contains("protected ", StringComparison.Ordinal))
            return MemberVisibility.Protected;
        if (member.Modifiers.Any(x => x.Equals("internal", StringComparison.OrdinalIgnoreCase)) ||
            member.FullString.Contains("internal ", StringComparison.Ordinal))
            return MemberVisibility.Internal;
        return MemberVisibility.Unknown;
    }

    private sealed class MemberSymbolIndex
    {
        private readonly Dictionary<int, MemberSymbolRow> _byId;
        private readonly Dictionary<string, List<MemberSymbolRow>> _byPathAndLine;
        private readonly Dictionary<string, List<MemberSymbolRow>> _byQualifiedName;

        public MemberSymbolIndex(IReadOnlyCollection<MemberSymbolRow> rows)
        {
            _byId = rows.ToDictionary(x => x.MemberId);
            _byPathAndLine = rows
                .GroupBy(x => $"{NormalizePath(x.FilePath)}:{x.Location.StartLineNumber}", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
            _byQualifiedName = rows
                .GroupBy(x => $"{x.Namespace}.{x.ObjectName}.{x.Name}", StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        }

        public MemberSymbolRow this[int id] => _byId[id];

        public MemberSymbolRow? TryResolve(ISymbol symbol)
        {
            var location = symbol.Locations.FirstOrDefault(x => x.IsInSource && x.SourceTree?.FilePath != null);
            if (location != null)
            {
                var span = location.GetLineSpan();
                var key = $"{NormalizePath(location.SourceTree!.FilePath)}:{span.StartLinePosition.Line}";
                if (_byPathAndLine.TryGetValue(key, out var lineCandidates))
                {
                    var match = lineCandidates.FirstOrDefault(candidate => Matches(candidate, symbol));
                    if (match != null) return match;
                }
            }

            var containingType = symbol.ContainingType;
            var qualifiedName = containingType == null
                ? string.Empty
                : $"{containingType.ContainingNamespace}.{containingType.Name}.{ResolveMemberName(symbol)}";
            return _byQualifiedName.TryGetValue(qualifiedName, out var candidates)
                ? candidates.FirstOrDefault(candidate => Matches(candidate, symbol))
                : null;
        }

        private static bool Matches(MemberSymbolRow candidate, ISymbol symbol)
        {
            return candidate.Kind == ResolveMemberKind(symbol) &&
                   (candidate.Name == ResolveMemberName(symbol) ||
                    symbol is IMethodSymbol { MethodKind: MethodKind.Constructor });
        }

        private static string ResolveMemberName(ISymbol symbol)
        {
            return symbol is IMethodSymbol { MethodKind: MethodKind.Constructor }
                ? symbol.ContainingType.Name
                : symbol.Name;
        }

        private static string ResolveMemberKind(ISymbol symbol)
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
    }

    private sealed record TracePath(
        IReadOnlyList<int> MemberIds,
        int TestDepth);

    private sealed record MemberSymbolRow(
        int MemberId,
        string Name,
        string Kind,
        string FullString,
        IReadOnlyList<string> Modifiers,
        bool IsTestMember,
        bool IsGenerated,
        string ObjectName,
        string Namespace,
        bool IsTestObject,
        string FilePath,
        string ProjectPath,
        bool IsTestProject,
        CodeLocation Location)
    {
        public bool IsProduction =>
            Kind is "method" or "property" or "constructor" &&
            !IsTestMember &&
            !IsGenerated &&
            !IsTestObject &&
            !IsTestProject &&
            !IsLikelyTestPath(FilePath) &&
            !IsLikelyTestPath(ProjectPath);

        public bool IsTestSupport =>
            IsTestObject ||
            IsTestProject ||
            IsLikelyTestPath(FilePath) ||
            IsLikelyTestPath(ProjectPath);
    }

    private static bool IsLikelyTestPath(string path)
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
