using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;

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
        IReadOnlySet<int> indexMemberIds,
        IReadOnlySet<int> testMemberIds,
        CancellationToken cancellationToken = default)
    {
        var solutionEntity = await _dbContext.CSharpSolutions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == solutionId, cancellationToken);
        if (solutionEntity == null || string.IsNullOrWhiteSpace(solutionEntity.FilePath)) return null;

        // Member rows are versioned and never removed, so the solution holds every version an
        // attempt ever produced. Only the ids the analysis pass observed describe the code on
        // disk; resolving against anything else can bind a call to another attempt's version.
        var memberRows = (await LoadMemberRowsAsync(solutionId, cancellationToken))
            .Where(x => indexMemberIds.Contains(x.MemberId))
            .ToList();
        var testMembers = memberRows
            .Where(x => x.IsTestMember && x.Kind == "method" && testMemberIds.Contains(x.MemberId))
            .OrderBy(x => x.MemberId)
            .ToList();
        if (testMembers.Count == 0) return [];

        var index = new RoslynMemberSymbolIndex(memberRows);
        var solution = await _workspace.OpenSolutionAsync(solutionEntity.FilePath, cancellationToken);

        var config = _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection;
        var mappings = new List<SourceTestMappingEntity>();
        foreach (var testMember in testMembers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = RoslynAnalysisUtilities.FindDocumentByPath(solution, testMember.FilePath);
            if (document == null) continue;

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (root == null || semanticModel == null) continue;

            var declaration = RoslynAnalysisUtilities.FindMemberDeclaration(root, testMember);
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
        RoslynMemberSymbolIndex index,
        TracePath path,
        int maxTestDepth,
        CancellationToken cancellationToken)
    {
        foreach (var node in GetTraceNodes(declaration))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = ResolveReferencedSymbol(node, semanticModel);
            if (symbol == null || IsAssertionInvocation(node, symbol)) continue;

            foreach (var targetSymbol in index.ResolveCandidateTargets(symbol, semanticModel.Compilation))
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

    private Task<List<RoslynMemberSymbolRow>> LoadMemberRowsAsync(
        int solutionId,
        CancellationToken cancellationToken)
    {
        return RoslynMemberSymbolIndex.LoadRowsAsync(_dbContext, solutionId, cancellationToken);
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
        RoslynMemberSymbolIndex index)
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
        return RoslynAnalysisUtilities.NormalizeSymbol(symbol);
    }

    private static bool IsAssertionInvocation(SyntaxNode node, ISymbol symbol)
    {
        return node is InvocationExpressionSyntax invocation &&
               CSharpAnalysisRules.IsAssertionInvocation(invocation, symbol);
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

    private static string ResolveEvidenceKind(IReadOnlyList<int> path, RoslynMemberSymbolIndex index)
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

    internal static string ResolveAccessPathStrategy(IReadOnlyList<int> path, RoslynMemberSymbolIndex index)
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
        RoslynMemberSymbolIndex index,
        string evidenceKind)
    {
        var names = path.Select(id => $"{index[id].Name}({id})");
        return $"{evidenceKind} Roslyn trace: {string.Join(" -> ", names)}.";
    }

    private static MemberVisibility ResolveVisibility(RoslynMemberSymbolRow member)
    {
        return MemberVisibilityResolver.Resolve(member.Modifiers, member.FullString, member.ObjectKind);
    }

    private sealed record TracePath(
        IReadOnlyList<int> MemberIds,
        int TestDepth);
}
