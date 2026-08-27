using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TestMap.Models.Experiment.Assertions;

namespace TestMap.Services.StaticAnalysis.Assertions;

public sealed class AssertionLineageSlicer
{
    private readonly RoslynProductionMemberResolver _resolver;
    private readonly AssertionLineageClassifier _classifier;

    public AssertionLineageSlicer(
        RoslynProductionMemberResolver resolver,
        AssertionLineageClassifier classifier)
    {
        _resolver = resolver;
        _classifier = classifier;
    }

    public Task<AssertionSliceResult> SliceAsync(
        IOperation operation,
        bool executionOperand,
        AssertionSliceContext context,
        CancellationToken cancellationToken = default)
    {
        return SliceCoreAsync(
            operation,
            executionOperand,
            context,
            0,
            new HashSet<string>(StringComparer.Ordinal),
            new SliceBudget(context.Policy.PathCap),
            cancellationToken);
    }

    private async Task<AssertionSliceResult> SliceCoreAsync(
        IOperation operation,
        bool executionOperand,
        AssertionSliceContext context,
        int depth,
        HashSet<string> visited,
        SliceBudget budget,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!budget.TryVisit())
            return TerminalUnresolved(
                operation,
                depth,
                AssertionLineageStepKind.Unsupported,
                AssertionLineageReasonCodes.PathCapExceeded);

        operation = Unwrap(operation);
        if (operation is IDynamicInvocationOperation or IDynamicMemberReferenceOperation)
            return TerminalUnresolved(
                operation,
                depth,
                AssertionLineageStepKind.Unsupported,
                AssertionLineageReasonCodes.DynamicInvocation);

        if (operation is IFlowCaptureReferenceOperation captureReference)
        {
            var definitions = context.ReachingDefinitions.GetCaptureDefinitions(captureReference.Id);
            var alternatives = new List<AssertionSliceResult>();
            foreach (var definition in definitions)
            {
                if (definition.Value == null)
                {
                    alternatives.Add(TerminalUnresolved(
                        operation,
                        depth,
                        AssertionLineageStepKind.Unsupported,
                        definition.ReasonCode));
                    continue;
                }
                alternatives.Add(await SliceCoreAsync(
                    definition.Value,
                    executionOperand,
                    context,
                    depth,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    budget,
                    cancellationToken));
            }
            return _classifier.CombineAlternatives(alternatives);
        }

        if (operation is ILocalReferenceOperation local)
            return await SliceLocalAsync(
                local,
                executionOperand,
                context,
                depth,
                visited,
                budget,
                cancellationToken);

        if (operation is IParameterReferenceOperation parameter)
        {
            var binding = context.ParameterBindings.FirstOrDefault(pair =>
                SymbolEqualityComparer.Default.Equals(pair.Key, parameter.Parameter));
            return binding.Key != null
                ? await SliceCoreAsync(
                    binding.Value,
                    executionOperand,
                    context.CallerContext ?? context,
                    depth,
                    visited,
                    budget,
                    cancellationToken)
                : TerminalTrivial(operation, depth);
        }

        if (operation is IConditionalOperation conditional)
        {
            // Deliberately do not inspect conditional.Condition: control dependence is out of scope.
            var alternatives = new List<AssertionSliceResult>
            {
                await SliceCoreAsync(
                    conditional.WhenTrue,
                    executionOperand,
                    context,
                    depth,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    budget,
                    cancellationToken)
            };
            if (conditional.WhenFalse != null)
                alternatives.Add(await SliceCoreAsync(
                    conditional.WhenFalse,
                    executionOperand,
                    context,
                    depth,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    budget,
                    cancellationToken));
            return _classifier.CombineAlternatives(alternatives);
        }

        if (operation is ICoalesceOperation coalesce)
        {
            return _classifier.CombineAlternatives(
            [
                await SliceCoreAsync(
                    coalesce.Value,
                    executionOperand,
                    context,
                    depth,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    budget,
                    cancellationToken),
                await SliceCoreAsync(
                    coalesce.WhenNull,
                    executionOperand,
                    context,
                    depth,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    budget,
                    cancellationToken)
            ]);
        }

        if (operation is IAnonymousFunctionOperation anonymous)
            return await SliceAnonymousFunctionAsync(
                anonymous,
                context,
                depth,
                visited,
                budget,
                cancellationToken);

        if (operation is IDelegateCreationOperation delegateCreation)
            return await SliceCoreAsync(
                delegateCreation.Target,
                executionOperand: true,
                context,
                depth,
                visited,
                budget,
                cancellationToken);

        var resolution = _resolver.Resolve(operation, context.SemanticModel.Compilation, context.MemberIndex);
        switch (resolution.Kind)
        {
            case RoslynMemberResolutionKind.Production:
                return TerminalTraced(operation, depth, resolution);
            case RoslynMemberResolutionKind.Ambiguous:
                return TerminalUnresolved(
                    operation,
                    depth,
                    AssertionLineageStepKind.AmbiguousDispatch,
                    AssertionLineageReasonCodes.AmbiguousDispatch);
            case RoslynMemberResolutionKind.Reflection:
                return TerminalUnresolved(
                    operation,
                    depth,
                    AssertionLineageStepKind.Unsupported,
                    AssertionLineageReasonCodes.Reflection);
            case RoslynMemberResolutionKind.Delegate:
                return await SliceDelegateInvocationAsync(
                    operation,
                    context,
                    depth,
                    visited,
                    budget,
                    cancellationToken);
            case RoslynMemberResolutionKind.TestSupport:
                return await SliceTestSupportAsync(
                    operation,
                    executionOperand,
                    resolution,
                    context,
                    depth,
                    visited,
                    budget,
                    cancellationToken);
        }

        if (operation.ConstantValue.HasValue ||
            operation is ILiteralOperation or IDefaultValueOperation or ITypeOfOperation or
                INameOfOperation or IInstanceReferenceOperation)
            return TerminalTrivial(operation, depth);

        var children = operation.ChildOperations.ToList();
        if (children.Count == 0)
            return TerminalTrivial(operation, depth);

        var components = new List<AssertionSliceResult>();
        foreach (var child in children)
            components.Add(await SliceCoreAsync(
                child,
                executionOperand,
                context,
                depth,
                new HashSet<string>(visited, StringComparer.Ordinal),
                budget,
                cancellationToken));
        return _classifier.CombineComponents(components);
    }

    private async Task<AssertionSliceResult> SliceLocalAsync(
        ILocalReferenceOperation local,
        bool executionOperand,
        AssertionSliceContext context,
        int depth,
        HashSet<string> visited,
        SliceBudget budget,
        CancellationToken cancellationToken)
    {
        if (depth >= context.Policy.MaxDepth)
            return TerminalUnresolved(
                local,
                depth,
                AssertionLineageStepKind.DepthLimit,
                AssertionLineageReasonCodes.DepthExceeded);

        var visitKey =
            $"local:{local.Local.ToDisplayString()}:{local.Syntax.SyntaxTree.FilePath}:{local.Syntax.SpanStart}:{depth}";
        if (!visited.Add(visitKey))
            return TerminalUnresolved(
                local,
                depth,
                AssertionLineageStepKind.Cycle,
                AssertionLineageReasonCodes.CycleDetected);

        var definitions = context.ReachingDefinitions.GetReachingDefinitions(local.Local, local);
        if (definitions.Count == 0)
            return TerminalUnresolved(
                local,
                depth,
                AssertionLineageStepKind.MissingSource,
                AssertionLineageReasonCodes.MissingDefinition);

        var alternatives = new List<AssertionSliceResult>();
        foreach (var definition in definitions)
        {
            if (definition.IsUnsupported || definition.Value == null)
            {
                alternatives.Add(TerminalUnresolved(
                    local,
                    depth + 1,
                    AssertionLineageStepKind.Unsupported,
                    definition.ReasonCode));
                continue;
            }

            alternatives.Add(await SliceCoreAsync(
                definition.Value,
                executionOperand,
                context,
                depth + 1,
                new HashSet<string>(visited, StringComparer.Ordinal),
                budget,
                cancellationToken));
        }

        var combined = _classifier.CombineAlternatives(alternatives);
        return Prepend(
            combined,
            CreateStep(
                local,
                AssertionLineageStepKind.LocalRead,
                depth,
                AssertionLineageStepOutcome.Continue,
                AssertionLineageReasonCodes.LineageContinued,
                $"Read local '{local.Local.Name}'."));
    }

    private async Task<AssertionSliceResult> SliceTestSupportAsync(
        IOperation operation,
        bool executionOperand,
        RoslynMemberResolution resolution,
        AssertionSliceContext context,
        int depth,
        HashSet<string> visited,
        SliceBudget budget,
        CancellationToken cancellationToken)
    {
        if (operation is IFieldReferenceOperation { Field.IsConst: false })
            return TerminalUnresolved(
                operation,
                depth,
                AssertionLineageStepKind.Unsupported,
                AssertionLineageReasonCodes.UnsupportedFieldFlow);

        if (depth >= context.Policy.MaxDepth)
            return TerminalUnresolved(
                operation,
                depth,
                AssertionLineageStepKind.DepthLimit,
                AssertionLineageReasonCodes.DepthExceeded);

        var symbol = resolution.Symbol;
        if (symbol is IPropertySymbol property) symbol = property.GetMethod;
        if (symbol is not IMethodSymbol helper)
            return TerminalUnresolved(
                operation,
                depth,
                AssertionLineageStepKind.Unsupported,
                AssertionLineageReasonCodes.UnsupportedFieldFlow);

        var helperKey = helper.GetDocumentationCommentId() ??
                        helper.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!visited.Add($"helper:{helperKey}"))
            return TerminalUnresolved(
                operation,
                depth,
                AssertionLineageStepKind.Cycle,
                AssertionLineageReasonCodes.CycleDetected);

        var declaration = RoslynAnalysisUtilities.TryGetTargetDeclaration(helper, cancellationToken);
        if (declaration == null)
            return TerminalUnresolved(
                operation,
                depth + 1,
                AssertionLineageStepKind.MissingSource,
                AssertionLineageReasonCodes.MissingSource);

        var document = context.Solution.GetDocument(declaration.SyntaxTree);
        var semanticModel = document == null
            ? null
            : await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null)
            return TerminalUnresolved(
                operation,
                depth + 1,
                AssertionLineageStepKind.MissingSource,
                AssertionLineageReasonCodes.SemanticModelUnavailable);

        ControlFlowGraph? graph;
        try
        {
            graph = ControlFlowGraph.Create(declaration, semanticModel, cancellationToken);
        }
        catch (ArgumentException)
        {
            return TerminalUnresolved(
                operation,
                depth + 1,
                AssertionLineageStepKind.Unsupported,
                AssertionLineageReasonCodes.MissingSource);
        }
        if (graph == null)
            return TerminalUnresolved(
                operation,
                depth + 1,
                AssertionLineageStepKind.MissingSource,
                AssertionLineageReasonCodes.MissingSource);

        var bindings = CreateParameterBindings(operation, helper);
        var helperContext = new AssertionSliceContext(
            context.Solution,
            semanticModel,
            graph,
            new ReachingDefinitionAnalysis(graph),
            context.MemberIndex,
            context.Policy,
            bindings,
            context);

        var returnValues = GetReturnValues(graph).ToList();
        List<AssertionSliceResult> alternatives = [];
        if (returnValues.Count > 0)
        {
            foreach (var returnValue in returnValues)
                alternatives.Add(await SliceCoreAsync(
                    returnValue,
                    false,
                    helperContext,
                    depth + 1,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    budget,
                    cancellationToken));
        }
        else if (executionOperand)
        {
            var roots = graph.Blocks
                .Where(block => block.IsReachable)
                .SelectMany(block => block.Operations)
                .ToList();
            foreach (var root in roots)
                alternatives.Add(await SliceCoreAsync(
                    root,
                    true,
                    helperContext,
                    depth + 1,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    budget,
                    cancellationToken));
        }

        var combined = _classifier.CombineAlternatives(alternatives);
        return Prepend(
            combined,
            CreateStep(
                operation,
                AssertionLineageStepKind.HelperCall,
                depth,
                AssertionLineageStepOutcome.Continue,
                AssertionLineageReasonCodes.LineageContinued,
                $"Follow test helper '{helper.ToDisplayString()}'.",
                resolution.MemberId));
    }

    private async Task<AssertionSliceResult> SliceAnonymousFunctionAsync(
        IAnonymousFunctionOperation anonymous,
        AssertionSliceContext context,
        int depth,
        HashSet<string> visited,
        SliceBudget budget,
        CancellationToken cancellationToken)
    {
        var components = new List<AssertionSliceResult>();
        foreach (var operation in anonymous.Body.ChildOperations)
            components.Add(await SliceCoreAsync(
                operation,
                true,
                context,
                depth,
                new HashSet<string>(visited, StringComparer.Ordinal),
                budget,
                cancellationToken));
        return _classifier.CombineComponents(components);
    }

    private async Task<AssertionSliceResult> SliceDelegateInvocationAsync(
        IOperation operation,
        AssertionSliceContext context,
        int depth,
        HashSet<string> visited,
        SliceBudget budget,
        CancellationToken cancellationToken)
    {
        if (operation is IInvocationOperation { Instance: not null } invocation)
            return await SliceCoreAsync(
                invocation.Instance,
                true,
                context,
                depth,
                visited,
                budget,
                cancellationToken);
        return TerminalUnresolved(
            operation,
            depth,
            AssertionLineageStepKind.Unsupported,
            AssertionLineageReasonCodes.UnknownDelegateTarget);
    }

    private static IReadOnlyDictionary<IParameterSymbol, IOperation> CreateParameterBindings(
        IOperation operation,
        IMethodSymbol helper)
    {
        var result = new Dictionary<IParameterSymbol, IOperation>(SymbolEqualityComparer.Default);
        if (operation is not IInvocationOperation invocation) return result;

        foreach (var argument in invocation.Arguments)
            if (argument.Parameter != null)
                result[argument.Parameter.OriginalDefinition] = argument.Value;

        if (helper.IsExtensionMethod && helper.Parameters.Length > 0 && invocation.Instance != null)
            result[helper.Parameters[0].OriginalDefinition] = invocation.Instance;
        return result;
    }

    private static IEnumerable<IOperation> GetReturnValues(ControlFlowGraph graph)
    {
        foreach (var block in graph.Blocks.Where(block => block.IsReachable))
        {
            foreach (var root in block.Operations)
            foreach (var @return in root.DescendantsAndSelf().OfType<IReturnOperation>())
                if (@return.ReturnedValue != null)
                    yield return @return.ReturnedValue;

            if (block.BranchValue != null &&
                block.FallThroughSuccessor?.Semantics == ControlFlowBranchSemantics.Return)
                yield return block.BranchValue;
        }
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation conversion:
                    operation = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    continue;
                case IAwaitOperation awaitOperation:
                    operation = awaitOperation.Operation;
                    continue;
                default:
                    return operation;
            }
        }
    }

    private static AssertionSliceResult TerminalTraced(
        IOperation operation,
        int depth,
        RoslynMemberResolution resolution)
    {
        var memberId = resolution.MemberId!.Value;
        return new AssertionSliceResult(
            AssertionLineageCategory.Traced,
            resolution.ReasonCode,
            depth,
            [memberId],
            [
                CreateStep(
                    operation,
                    AssertionLineageStepKind.ProductionMember,
                    depth,
                    AssertionLineageStepOutcome.Traced,
                    resolution.ReasonCode,
                    $"Resolved production member {memberId}.",
                    memberId,
                    resolution.Symbol?.ToDisplayString())
            ]);
    }

    private static AssertionSliceResult TerminalTrivial(IOperation operation, int depth)
    {
        return AssertionSliceResult.Trivial(
            AssertionLineageReasonCodes.AllInputsTestLocal,
            depth,
            [
                CreateStep(
                    operation,
                    operation.ConstantValue.HasValue
                        ? AssertionLineageStepKind.Literal
                        : AssertionLineageStepKind.TestLocal,
                    depth,
                    AssertionLineageStepOutcome.Trivial,
                    AssertionLineageReasonCodes.AllInputsTestLocal,
                    "Lineage terminates in a literal or test-local value.")
            ]);
    }

    private static AssertionSliceResult TerminalUnresolved(
        IOperation operation,
        int depth,
        AssertionLineageStepKind stepKind,
        string reasonCode)
    {
        return AssertionSliceResult.Unresolved(
            reasonCode,
            depth,
            [
                CreateStep(
                    operation,
                    stepKind,
                    depth,
                    AssertionLineageStepOutcome.Unresolved,
                    reasonCode,
                    $"Lineage is unresolved: {reasonCode}.")
            ]);
    }

    private static AssertionSliceResult Prepend(
        AssertionSliceResult result,
        AssertionLineageStep step)
    {
        return result with { Steps = new[] { step }.Concat(result.Steps).ToList() };
    }

    private static AssertionLineageStep CreateStep(
        IOperation operation,
        AssertionLineageStepKind kind,
        int depth,
        AssertionLineageStepOutcome outcome,
        string reasonCode,
        string summary,
        int? memberId = null,
        string? symbolDisplay = null)
    {
        var span = operation.Syntax.GetLocation().GetLineSpan();
        return new AssertionLineageStep
        {
            StepKind = kind,
            Depth = depth,
            Outcome = outcome,
            ReasonCode = reasonCode,
            Summary = summary,
            MemberId = memberId,
            SymbolDisplay = symbolDisplay ?? string.Empty,
            FilePath = operation.Syntax.SyntaxTree.FilePath,
            StartLine = span.StartLinePosition.Line,
            StartColumn = span.StartLinePosition.Character,
            EndLine = span.EndLinePosition.Line,
            EndColumn = span.EndLinePosition.Character
        };
    }

    private sealed class SliceBudget(int pathCap)
    {
        private int _visits;
        public bool TryVisit() => ++_visits <= pathCap;
    }
}

public sealed record AssertionSliceContext(
    Solution Solution,
    SemanticModel SemanticModel,
    ControlFlowGraph ControlFlowGraph,
    ReachingDefinitionAnalysis ReachingDefinitions,
    RoslynMemberSymbolIndex MemberIndex,
    AssertionLineagePolicy Policy,
    IReadOnlyDictionary<IParameterSymbol, IOperation> ParameterBindings,
    AssertionSliceContext? CallerContext = null);
