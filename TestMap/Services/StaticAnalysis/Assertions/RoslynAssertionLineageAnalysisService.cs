using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment.Assertions;
using TestMap.Persistence.Ef;

namespace TestMap.Services.StaticAnalysis.Assertions;

public sealed class RoslynAssertionLineageAnalysisService : IAssertionLineageAnalysisService
{
    private readonly TestMapDbContext _dbContext;
    private readonly IStaticAnalysisWorkspace _workspace;
    private readonly AssertionPatternCatalog _catalog;
    private readonly AssertionOperandExtractor _operandExtractor;
    private readonly AssertionLineageSlicer _slicer;
    private readonly AssertionLineageClassifier _classifier;

    public RoslynAssertionLineageAnalysisService(
        TestMapDbContext dbContext,
        IStaticAnalysisWorkspace workspace,
        AssertionPatternCatalog catalog,
        AssertionOperandExtractor operandExtractor,
        AssertionLineageSlicer slicer,
        AssertionLineageClassifier classifier)
    {
        _dbContext = dbContext;
        _workspace = workspace;
        _catalog = catalog;
        _operandExtractor = operandExtractor;
        _slicer = slicer;
        _classifier = classifier;
    }

    public async Task<AssertionLineageAnalysisResult> AnalyzeAsync(
        AssertionLineageAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        if (!request.Policy.Enabled)
            return AssertionLineageAnalysisResult.Unavailable(
                request.Policy,
                AssertionLineageReasonCodes.AnalysisDisabled,
                "Assertion-lineage analysis is disabled by the effective experiment policy.",
                startedAt,
                DateTime.UtcNow);

        var solutionRow = await _dbContext.CSharpSolutions
            .AsNoTracking()
            .FirstOrDefaultAsync(solution => solution.Id == request.SolutionId, cancellationToken);
        if (solutionRow == null || string.IsNullOrWhiteSpace(solutionRow.FilePath))
            return AssertionLineageAnalysisResult.Unavailable(
                request.Policy,
                AssertionLineageReasonCodes.SemanticProjectUnavailable,
                $"Solution {request.SolutionId} is unavailable.",
                startedAt,
                DateTime.UtcNow);

        Solution solution;
        List<RoslynMemberSymbolRow> memberRows;
        try
        {
            memberRows = await RoslynMemberSymbolIndex.LoadRowsAsync(
                _dbContext,
                request.SolutionId,
                cancellationToken);
            solution = await _workspace.OpenSolutionAsync(solutionRow.FilePath, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return AssertionLineageAnalysisResult.Unavailable(
                request.Policy,
                AssertionLineageReasonCodes.ProjectLoadFailure,
                exception.Message,
                startedAt,
                DateTime.UtcNow);
        }

        var memberIndex = new RoslynMemberSymbolIndex(memberRows);
        var summaries = new List<GeneratedTestAssertionSummary>();
        foreach (var testMemberId in request.TestMemberIds.Distinct().Order())
        {
            cancellationToken.ThrowIfCancellationRequested();
            summaries.Add(await AnalyzeTestMemberAsync(
                testMemberId,
                request.IntendedSourceMemberId,
                request.Policy,
                solution,
                memberIndex,
                cancellationToken));
        }

        stopwatch.Stop();
        var completedAt = DateTime.UtcNow;
        return new AssertionLineageAnalysisResult
        {
            Available = true,
            Policy = request.Policy,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            AnalysisDurationMs = stopwatch.Elapsed.TotalMilliseconds,
            TestSummaries = summaries
        };
    }

    private async Task<GeneratedTestAssertionSummary> AnalyzeTestMemberAsync(
        int testMemberId,
        int intendedSourceMemberId,
        AssertionLineagePolicy policy,
        Solution solution,
        RoslynMemberSymbolIndex memberIndex,
        CancellationToken cancellationToken)
    {
        if (!memberIndex.TryGet(testMemberId, out var member))
            return UnavailableSummary(
                testMemberId,
                string.Empty,
                string.Empty,
                AssertionLineageReasonCodes.GeneratedTestMemberUnresolved);

        var document = RoslynAnalysisUtilities.FindDocumentByPath(solution, member.FilePath);
        if (document == null)
            return UnavailableSummary(
                testMemberId,
                member.Name,
                member.FilePath,
                AssertionLineageReasonCodes.SourceUnavailable,
                member.ContentHash);

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root == null || semanticModel == null)
            return UnavailableSummary(
                testMemberId,
                member.Name,
                member.FilePath,
                AssertionLineageReasonCodes.SemanticModelUnavailable,
                member.ContentHash);

        var declaration = RoslynAnalysisUtilities.FindMemberDeclaration(root, member);
        if (declaration == null)
            return UnavailableSummary(
                testMemberId,
                member.Name,
                member.FilePath,
                AssertionLineageReasonCodes.MissingSource,
                member.ContentHash);

        ControlFlowGraph? graph;
        try
        {
            graph = ControlFlowGraph.Create(declaration, semanticModel, cancellationToken);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return UnavailableSummary(
                testMemberId,
                member.Name,
                member.FilePath,
                AssertionLineageReasonCodes.AnalysisFailure,
                member.ContentHash,
                exception.Message);
        }
        if (graph == null)
            return UnavailableSummary(
                testMemberId,
                member.Name,
                member.FilePath,
                AssertionLineageReasonCodes.AnalysisFailure,
                member.ContentHash,
                "Roslyn did not produce a control-flow graph.");

        var declarationHash = ComputeHash(declaration.ToFullString());
        var reachingDefinitions = new ReachingDefinitionAnalysis(graph);
        var sliceContext = new AssertionSliceContext(
            solution,
            semanticModel,
            graph,
            reachingDefinitions,
            memberIndex,
            policy,
            new Dictionary<IParameterSymbol, IOperation>(SymbolEqualityComparer.Default));
        var candidates = declaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation =>
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
                var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                var operation = semanticModel.GetOperation(invocation, cancellationToken) as IInvocationOperation;
                var pattern = operation == null
                    ? _catalog.Match(invocation, symbol)
                    : _catalog.Match(operation);
                return new AssertionCandidate(invocation, operation, pattern);
            })
            .Where(candidate => candidate.Pattern != null)
            .OrderBy(candidate => candidate.Syntax.SpanStart)
            .ToList();

        var observations = new List<AssertionObservation>();
        for (var ordinal = 0; ordinal < candidates.Count; ordinal++)
        {
            var candidate = candidates[ordinal];
            observations.Add(await AnalyzeAssertionAsync(
                candidate,
                ordinal,
                intendedSourceMemberId,
                sliceContext,
                cancellationToken));
        }

        var recognized = observations.Count;
        return new GeneratedTestAssertionSummary
        {
            TestMemberId = testMemberId,
            TestMethodName = member.Name,
            TestFilePath = member.FilePath,
            // Fingerprint the declaration that was actually analysed, not the member row.
            // A member row is identified by object, kind, name and signature -- deliberately not
            // by its body -- so every attempt that emits a same-named test maps to one row whose
            // body is overwritten by whichever attempt ran last. Hashing the row would therefore
            // give six different generated tests one identical, useless fingerprint. Hashing the
            // declaration text makes each attempt's test distinguishable and matches what the
            // model lane already records for its generated code.
            TestMemberContentHash = declarationHash,
            FallbackIdentityHash = ComputeHash($"{testMemberId}:{declarationHash}"),
            Status = recognized == 0
                ? GeneratedTestAssertionStatus.NoRecognizedAssertions
                : GeneratedTestAssertionStatus.Classified,
            StatusReason = recognized == 0 ? "No supported or fallback assertion terminal was recognized." : null,
            RecognizedAssertionCount = recognized,
            UnrecognizedAssertionCount = 0,
            TracedAssertionCount = observations.Count(observation =>
                observation.Category == AssertionLineageCategory.Traced),
            TrivialAssertionCount = observations.Count(observation =>
                observation.Category == AssertionLineageCategory.Trivial),
            UnresolvedAssertionCount = observations.Count(observation =>
                observation.Category == AssertionLineageCategory.Unresolved),
            Observations = observations
        };
    }

    private async Task<AssertionObservation> AnalyzeAssertionAsync(
        AssertionCandidate candidate,
        int ordinal,
        int intendedSourceMemberId,
        AssertionSliceContext context,
        CancellationToken cancellationToken)
    {
        var pattern = candidate.Pattern!;
        AssertionSliceResult classification;
        var indexedSteps = new List<AssertionLineageStep>();
        // The assertion terminal is only a marker. When it binds, operands come from the
        // operation tree; when it does not, they come from the argument syntax. Either way the
        // operands are what get traced, so an unbound terminal no longer discards lineage the
        // semantic model can still resolve.
        var operands = pattern.IsSupported && candidate.Operation != null
            ? _operandExtractor.Extract(candidate.Operation, pattern)
            : _operandExtractor.ExtractFromSyntax(candidate.Syntax, context.SemanticModel);

        if (operands.Count == 0)
        {
            classification = AssertionSliceResult.Unresolved(
                AssertionLineageReasonCodes.UnsupportedAssertionShape,
                0);
            indexedSteps.Add(CreateUnsupportedAssertionStep(candidate.Syntax));
        }
        else
        {
            var slices = new List<AssertionSliceResult>();
            foreach (var operand in operands)
            {
                var slice = await _slicer.SliceAsync(
                    operand.Operation,
                    operand.IsExecution,
                    context,
                    cancellationToken);
                IndexSteps(slice.Steps, operand.Index, indexedSteps);
                slices.Add(slice);
            }

            classification = _classifier.CombineComponents(slices);
        }

        var productionMembers = classification.ProductionMemberIds;
        var location = candidate.Syntax.GetLocation().GetLineSpan();
        return new AssertionObservation
        {
            Ordinal = ordinal,
            Framework = pattern.Framework,
            AssertionMethod = pattern.MethodDisplay,
            RecognitionKind = pattern.RecognitionKind,
            FilePath = candidate.Syntax.SyntaxTree.FilePath,
            StartLine = location.StartLinePosition.Line,
            StartColumn = location.StartLinePosition.Character,
            EndLine = location.EndLinePosition.Line,
            EndColumn = location.EndLinePosition.Character,
            ExpressionHash = ComputeHash(
                $"{candidate.Syntax.WithoutTrivia()}:{candidate.Syntax.SpanStart}:{candidate.Syntax.Span.Length}"),
            Category = classification.Category,
            ResolutionCode = classification.ReasonCode,
            DepthReached = classification.DepthReached,
            TargetRelation = ResolveTargetRelation(productionMembers, intendedSourceMemberId),
            TraceSummary = BuildTraceSummary(classification, productionMembers),
            Steps = indexedSteps
        };
    }

    /// <summary>
    /// Lays a slice's steps out as audited lineage paths. A combined slice concatenates the
    /// steps of every component it explored, so the flat sequence holds one terminal per
    /// component. Each terminal closes a path and the next step opens a new one, which keeps
    /// the audit invariant that a path has contiguous step indexes and ends in exactly one
    /// terminal.
    /// </summary>
    private static void IndexSteps(
        IReadOnlyList<AssertionLineageStep> source,
        int inputIndex,
        ICollection<AssertionLineageStep> destination)
    {
        var pathIndex = 0;
        var stepIndex = 0;
        destination.Add(CreateAssertionInputStep(inputIndex, pathIndex, stepIndex++));

        foreach (var step in source)
        {
            step.InputIndex = inputIndex;
            step.PathIndex = pathIndex;
            step.StepIndex = stepIndex++;
            destination.Add(step);

            if (step.Outcome == AssertionLineageStepOutcome.Continue) continue;

            pathIndex++;
            stepIndex = 0;
        }

        // A trailing run of non-terminating steps would leave the final path open, and an
        // input whose slice produced nothing would leave the opening step alone. Close either
        // case so every emitted path ends in exactly one terminal.
        if (stepIndex == 0) return;

        destination.Add(new AssertionLineageStep
        {
            InputIndex = inputIndex,
            PathIndex = pathIndex,
            StepIndex = stepIndex,
            StepKind = AssertionLineageStepKind.Unsupported,
            Outcome = AssertionLineageStepOutcome.Unresolved,
            ReasonCode = AssertionLineageReasonCodes.MissingDefinition,
            Summary = $"Assertion input {inputIndex} path {pathIndex} produced no terminal step."
        });
    }

    private static AssertionLineageStep CreateAssertionInputStep(
        int inputIndex,
        int pathIndex,
        int stepIndex) =>
        new()
        {
            InputIndex = inputIndex,
            PathIndex = pathIndex,
            StepIndex = stepIndex,
            StepKind = AssertionLineageStepKind.AssertionInput,
            Outcome = AssertionLineageStepOutcome.Continue,
            ReasonCode = AssertionLineageReasonCodes.LineageContinued,
            Summary = $"Start assertion input {inputIndex}."
        };

    private static AssertionLineageStep CreateUnsupportedAssertionStep(
        InvocationExpressionSyntax syntax)
    {
        var location = syntax.GetLocation().GetLineSpan();
        return new AssertionLineageStep
        {
            InputIndex = 0,
            PathIndex = 0,
            StepIndex = 0,
            StepKind = AssertionLineageStepKind.Unsupported,
            Outcome = AssertionLineageStepOutcome.Unresolved,
            ReasonCode = AssertionLineageReasonCodes.UnsupportedAssertionShape,
            Summary = "The assertion occurrence is recognized, but its observed operands are not catalogued.",
            FilePath = syntax.SyntaxTree.FilePath,
            StartLine = location.StartLinePosition.Line,
            StartColumn = location.StartLinePosition.Character,
            EndLine = location.EndLinePosition.Line,
            EndColumn = location.EndLinePosition.Character
        };
    }

    private static AssertionTargetRelation ResolveTargetRelation(
        IReadOnlyList<int> productionMemberIds,
        int intendedSourceMemberId)
    {
        if (productionMemberIds.Count == 0) return AssertionTargetRelation.NoProduction;
        var hasCandidate = productionMemberIds.Contains(intendedSourceMemberId);
        if (hasCandidate && productionMemberIds.Count == 1) return AssertionTargetRelation.Candidate;
        if (hasCandidate) return AssertionTargetRelation.MixedProduction;
        return AssertionTargetRelation.OtherProduction;
    }

    private static string BuildTraceSummary(
        AssertionSliceResult result,
        IReadOnlyList<int> productionMembers)
    {
        var members = productionMembers.Count == 0
            ? "none"
            : string.Join(",", productionMembers.Order());
        return $"Category={result.Category}; Reason={result.ReasonCode}; Depth={result.DepthReached}; ProductionMembers={members}.";
    }

    private static GeneratedTestAssertionSummary UnavailableSummary(
        int? testMemberId,
        string testMethodName,
        string filePath,
        string reasonCode,
        string contentHash = "",
        string? detail = null)
    {
        return new GeneratedTestAssertionSummary
        {
            TestMemberId = testMemberId,
            TestMethodName = testMethodName,
            TestFilePath = filePath,
            TestMemberContentHash = contentHash,
            FallbackIdentityHash = ComputeHash($"{testMemberId}:{testMethodName}:{filePath}:{contentHash}"),
            Status = GeneratedTestAssertionStatus.Unavailable,
            StatusReason = string.IsNullOrWhiteSpace(detail) ? reasonCode : $"{reasonCode}: {detail}",
            RecognizedAssertionCount = null,
            UnrecognizedAssertionCount = null,
            TracedAssertionCount = null,
            TrivialAssertionCount = null,
            UnresolvedAssertionCount = null
        };
    }

    private static string ComputeHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private sealed record AssertionCandidate(
        InvocationExpressionSyntax Syntax,
        IInvocationOperation? Operation,
        AssertionPatternMatch? Pattern);
}
