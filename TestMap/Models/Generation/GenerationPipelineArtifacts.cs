namespace TestMap.Models.Generation;

public sealed class GenerationPipelineArtifacts
{
    public string? Scenario { get; set; }
    public string? MethodName { get; set; }
    public ArrangePlan? ArrangePlan { get; set; }
    public InputPlan? InputPlan { get; set; }
    public ActionPlan? ActionPlan { get; set; }
    public AssertionPlan? AssertionPlan { get; set; }
    public ContextGraph? ContextGraph { get; set; }
    public IReadOnlyList<ContextResolutionResult> ContextResolution { get; set; } = [];
}

public sealed class ContextGraph
{
    public string CandidateId { get; init; } = string.Empty;
    public IReadOnlyList<ContextGraphNode> Nodes { get; init; } = [];
}

public sealed class ContextGraphNode
{
    public string NodeId { get; init; } = string.Empty;
    public string NodeType { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string? VariableName { get; init; }
    public IReadOnlyList<string> DependsOnNodeIds { get; init; } = [];
    public string SourceSummary { get; init; } = string.Empty;
    public string ConstructionHint { get; init; } = string.Empty;
    public bool RequiresMocking { get; init; }
    public bool IsResolved { get; init; }
}

public sealed class ContextResolutionResult
{
    public string NodeId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string CodeSnippet { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}
