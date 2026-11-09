using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TestMap.Models.Experiment.Assertions;

namespace TestMap.Services.StaticAnalysis.Assertions;

public sealed record ReachingDefinition(
    IOperation? Value,
    int BlockOrdinal,
    int Position,
    bool IsUnsupported,
    string ReasonCode);

public sealed class ReachingDefinitionAnalysis
{
    private readonly ControlFlowGraph _graph;
    private readonly Dictionary<int, DefinitionState> _entryStates = [];
    private readonly Dictionary<int, IReadOnlyList<Write>> _writesByBlock = [];
    private readonly Dictionary<CaptureId, IReadOnlyList<ReachingDefinition>> _captureDefinitions = [];

    public ReachingDefinitionAnalysis(ControlFlowGraph graph)
    {
        _graph = graph;
        IndexWrites();
        ComputeFixedPoint();
    }

    public IReadOnlyList<ReachingDefinition> GetReachingDefinitions(
        ILocalSymbol local,
        IOperation use)
    {
        var block = FindContainingBlock(use);
        if (block == null) return [];

        var state = _entryStates.TryGetValue(block.Ordinal, out var entry)
            ? entry.Clone()
            : new DefinitionState();
        foreach (var write in _writesByBlock[block.Ordinal]
                     .Where(candidate => candidate.Position <= use.Syntax.SpanStart)
                     .OrderBy(candidate => candidate.Position))
            state.Apply(write);

        return state.Get(local)
            .OrderBy(definition => definition.BlockOrdinal)
            .ThenBy(definition => definition.Position)
            .ToList();
    }

    public IReadOnlyList<ReachingDefinition> GetCaptureDefinitions(CaptureId captureId)
    {
        return _captureDefinitions.TryGetValue(captureId, out var definitions)
            ? definitions
            : [];
    }

    private void IndexWrites()
    {
        var captures = new Dictionary<CaptureId, List<ReachingDefinition>>();
        foreach (var block in _graph.Blocks)
        {
            var writes = new List<Write>();
            foreach (var operation in EnumerateOperations(block))
            {
                switch (operation)
                {
                    case IVariableDeclaratorOperation declarator:
                    {
                        var initializer = declarator.GetVariableInitializer();
                        if (initializer?.Value == null) break;
                        writes.Add(Write.ForLocal(
                            declarator.Symbol,
                            CreateDefinition(initializer.Value, block, false, string.Empty)));
                        break;
                    }
                    case ISimpleAssignmentOperation assignment
                        when assignment.Target is ILocalReferenceOperation local:
                        writes.Add(Write.ForLocal(
                            local.Local,
                            CreateDefinition(assignment.Value, block, false, string.Empty)));
                        break;
                    case ICompoundAssignmentOperation compound
                        when compound.Target is ILocalReferenceOperation local:
                        writes.Add(Write.ForLocal(
                            local.Local,
                            CreateDefinition(
                                null,
                                block,
                                true,
                                AssertionLineageReasonCodes.UnsupportedWrite,
                                compound.Syntax.Span.End)));
                        break;
                    case IIncrementOrDecrementOperation increment
                        when increment.Target is ILocalReferenceOperation local:
                        writes.Add(Write.ForLocal(
                            local.Local,
                            CreateDefinition(
                                null,
                                block,
                                true,
                                AssertionLineageReasonCodes.UnsupportedWrite,
                                increment.Syntax.Span.End)));
                        break;
                    case IArgumentOperation argument
                        when argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out &&
                             argument.Value is ILocalReferenceOperation local:
                        writes.Add(Write.ForLocal(
                            local.Local,
                            CreateDefinition(
                                null,
                                block,
                                true,
                                AssertionLineageReasonCodes.UnsupportedAlias,
                                argument.Syntax.Span.End)));
                        break;
                    case IFlowCaptureOperation capture:
                    {
                        var definition = CreateDefinition(capture.Value, block, false, string.Empty);
                        if (!captures.TryGetValue(capture.Id, out var captureRows))
                        {
                            captureRows = [];
                            captures[capture.Id] = captureRows;
                        }
                        captureRows.Add(definition);
                        break;
                    }
                }
            }

            _writesByBlock[block.Ordinal] = writes
                .GroupBy(write => new
                {
                    Symbol = write.Local?.ToDisplayString(),
                    write.Definition.Position,
                    write.Definition.ReasonCode
                })
                .Select(group => group.First())
                .OrderBy(write => write.Position)
                .ToList();
        }

        foreach (var (captureId, definitions) in captures)
            _captureDefinitions[captureId] = definitions
                .Distinct()
                .OrderBy(definition => definition.BlockOrdinal)
                .ThenBy(definition => definition.Position)
                .ToList();
    }

    private void ComputeFixedPoint()
    {
        foreach (var block in _graph.Blocks)
            _entryStates[block.Ordinal] = new DefinitionState();

        var exitStates = _graph.Blocks.ToDictionary(
            block => block.Ordinal,
            _ => new DefinitionState());
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var block in _graph.Blocks.Where(block => block.IsReachable))
            {
                var incoming = DefinitionState.Merge(
                    block.Predecessors
                        .Where(predecessor => predecessor.Source.IsReachable)
                        .Select(predecessor => exitStates[predecessor.Source.Ordinal]));
                if (!_entryStates[block.Ordinal].SetEquals(incoming))
                {
                    _entryStates[block.Ordinal] = incoming;
                    changed = true;
                }

                var outgoing = incoming.Clone();
                foreach (var write in _writesByBlock[block.Ordinal])
                    outgoing.Apply(write);
                if (!exitStates[block.Ordinal].SetEquals(outgoing))
                {
                    exitStates[block.Ordinal] = outgoing;
                    changed = true;
                }
            }
        }
    }

    private BasicBlock? FindContainingBlock(IOperation use)
    {
        return _graph.Blocks
            .Where(block => block.IsReachable)
            .FirstOrDefault(block => EnumerateOperations(block).Any(operation =>
                operation.Kind == use.Kind &&
                operation.Syntax.SyntaxTree == use.Syntax.SyntaxTree &&
                operation.Syntax.Span == use.Syntax.Span));
    }

    private static IEnumerable<IOperation> EnumerateOperations(BasicBlock block)
    {
        foreach (var root in block.Operations)
        foreach (var operation in root.DescendantsAndSelf())
            yield return operation;

        if (block.BranchValue != null)
            foreach (var operation in block.BranchValue.DescendantsAndSelf())
                yield return operation;
    }

    private static ReachingDefinition CreateDefinition(
        IOperation? value,
        BasicBlock block,
        bool unsupported,
        string reasonCode,
        int? position = null)
    {
        return new ReachingDefinition(
            value,
            block.Ordinal,
            position ?? value?.Syntax.Span.End ?? 0,
            unsupported,
            reasonCode);
    }

    private sealed record Write(
        ILocalSymbol? Local,
        ReachingDefinition Definition)
    {
        public int Position => Definition.Position;

        public static Write ForLocal(ILocalSymbol local, ReachingDefinition definition) =>
            new(local, definition);
    }

    private sealed class DefinitionState
    {
        private readonly Dictionary<ILocalSymbol, HashSet<ReachingDefinition>> _locals =
            new(SymbolEqualityComparer.Default);

        public IReadOnlyCollection<ReachingDefinition> Get(ILocalSymbol local) =>
            _locals.TryGetValue(local, out var definitions) ? definitions : [];

        public void Apply(Write write)
        {
            if (write.Local == null) return;
            _locals[write.Local] = [write.Definition];
        }

        public DefinitionState Clone()
        {
            var clone = new DefinitionState();
            foreach (var (local, definitions) in _locals)
                clone._locals[local] = [..definitions];
            return clone;
        }

        public bool SetEquals(DefinitionState other)
        {
            if (_locals.Count != other._locals.Count) return false;
            return _locals.All(pair =>
                other._locals.TryGetValue(pair.Key, out var definitions) &&
                pair.Value.SetEquals(definitions));
        }

        public static DefinitionState Merge(IEnumerable<DefinitionState> states)
        {
            var result = new DefinitionState();
            foreach (var state in states)
            foreach (var (local, definitions) in state._locals)
            {
                if (!result._locals.TryGetValue(local, out var merged))
                {
                    merged = [];
                    result._locals[local] = merged;
                }
                merged.UnionWith(definitions);
            }
            return result;
        }
    }
}
