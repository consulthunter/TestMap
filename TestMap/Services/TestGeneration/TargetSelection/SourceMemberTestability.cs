namespace TestMap.Services.TestGeneration.TargetSelection;

public enum MemberVisibility
{
    Public,
    Internal,
    Protected,
    Private,
    ExplicitInterface,
    Unknown
}

public enum TestAccessStrategy
{
    DirectPublicCall,
    DirectInternalCall,
    ProtectedSubclassHarness,
    PublicCallerPath,
    InternalCallerPath,
    HelperMediatedPath,
    ReflectionFallback,
    NotReasonablyTestable,
    Unknown
}

public enum TestEvidenceStatus
{
    DirectlyMappedToTest,
    IndirectlyMappedToTest,
    ObservedCovered,
    ObservedMutationExercised,
    ObservedUntested,
    EvidenceUnavailable
}

public sealed record SourceMemberTestability
{
    public required int SourceMemberId { get; init; }
    public required MemberVisibility Visibility { get; init; }
    public required IReadOnlyList<TestMappingPath> TestMappings { get; init; }
    public required IReadOnlyList<AccessPath> AccessPaths { get; init; }
    public required IReadOnlyList<TestEvidenceStatus> EvidenceStatuses { get; init; }
    public required IReadOnlyList<ContextBinding> SetupBindings { get; init; }
}

public sealed record TestMappingPath
{
    public required int TestMemberId { get; init; }
    public required int SourceMemberId { get; init; }
    public required IReadOnlyList<int> PathMemberIds { get; init; }
    public required bool IsDirect { get; init; }
    public required bool IsObservedByCoverage { get; init; }
    public required bool IsObservedByMutation { get; init; }
}

public sealed record AccessPath
{
    public required int TargetMemberId { get; init; }
    public required int EntrypointMemberId { get; init; }
    public required IReadOnlyList<int> PathMemberIds { get; init; }
    public required TestAccessStrategy Strategy { get; init; }
    public required bool IsLegalFromTest { get; init; }
    public required bool RequiresReflection { get; init; }
    public int Distance => Math.Max(0, PathMemberIds.Count - 1);
}

public sealed record ContextBinding
{
    public required string NeedId { get; init; }
    public required string NeedKind { get; init; }
    public string? RequiredType { get; init; }
    public string? BindingExpression { get; init; }
    public string? BindingKind { get; init; }
    public int? SourceMemberId { get; init; }
    public string? Reason { get; init; }
    public bool IsPreferred { get; init; }
}
