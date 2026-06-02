namespace TestMap.Models.Generation;

public sealed class CandidateMethodMetadata
{
    public IReadOnlyList<CandidateTestIntention> TestIntentions { get; init; } = [];
    public CandidateTypeConstructionPlan TypeConstruction { get; init; } = new();
    public string TestIntentionsSummary { get; init; } = "No branch-derived test intentions were identified.";
    public string TypeConstructionSummary { get; init; } = "No type construction guidance was identified.";
}

public sealed class CandidateTestIntention
{
    public string Kind { get; init; } = string.Empty;
    public string SourceText { get; init; } = string.Empty;
    public string Intention { get; init; } = string.Empty;
}

public sealed class CandidateTypeConstructionPlan
{
    public string SutTypeName { get; init; } = string.Empty;
    public bool RequiresSutInstance { get; init; } = true;
    public IReadOnlyList<CandidateTypeConstructionItem> SutDependencies { get; init; } = [];
    public IReadOnlyList<CandidateTypeConstructionItem> MethodParameters { get; init; } = [];
}

public sealed class CandidateTypeConstructionItem
{
    public string Name { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string Strategy { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<CandidateTypeConstructionItem> Children { get; init; } = [];
}
