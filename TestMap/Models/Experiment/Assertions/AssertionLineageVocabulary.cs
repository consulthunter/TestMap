namespace TestMap.Models.Experiment.Assertions;

public enum AssertionLineageCategory
{
    Traced,
    Trivial,
    Unresolved
}

public enum AssertionMeasurementStatus
{
    Complete,
    Partial,
    Unavailable,
    NotApplicable,
    NotMeasured
}

public enum GeneratedTestAssertionStatus
{
    Classified,
    NoRecognizedAssertions,
    Unavailable
}

public enum AssertionRecognitionKind
{
    Semantic,
    SyntacticFallback
}

public enum AssertionTargetRelation
{
    Candidate,
    OtherProduction,
    MixedProduction,
    NoProduction,
    Unknown
}

public enum AssertionLineageStepKind
{
    AssertionInput,
    LocalRead,
    ReachingDefinition,
    Expression,
    HelperCall,
    HelperReturn,
    ProductionMember,
    Literal,
    TestLocal,
    DepthLimit,
    Cycle,
    AmbiguousDispatch,
    Unsupported,
    MissingSource
}

public enum AssertionLineageStepOutcome
{
    Continue,
    Traced,
    Trivial,
    Unresolved
}

public static class AssertionLineageReasonCodes
{
    /// <summary>
    /// Marks a step that advances a lineage path without ending it — the synthetic input
    /// marker, a local read, a followed test helper. Every step carries a stable reason code,
    /// so waypoints need one even though they carry no outcome.
    /// </summary>
    public const string LineageContinued = nameof(LineageContinued);

    /// <summary>
    /// The analyzer produced observations the integrity audit rejected. Recorded in place of
    /// the rejected evidence so the attempt still carries an honest, self-describing status
    /// instead of losing its results to a failed enrichment.
    /// </summary>
    public const string AssertionAuditFailed = nameof(AssertionAuditFailed);

    public const string ProductionInvocation = nameof(ProductionInvocation);
    public const string ProductionConstruction = nameof(ProductionConstruction);
    public const string ProductionValueAccess = nameof(ProductionValueAccess);
    public const string AllInputsTestLocal = nameof(AllInputsTestLocal);
    public const string DepthExceeded = nameof(DepthExceeded);
    public const string CycleDetected = nameof(CycleDetected);
    public const string AmbiguousDefinitions = nameof(AmbiguousDefinitions);
    public const string AmbiguousDispatch = nameof(AmbiguousDispatch);
    public const string DynamicInvocation = nameof(DynamicInvocation);
    public const string Reflection = nameof(Reflection);
    public const string UnknownDelegateTarget = nameof(UnknownDelegateTarget);
    public const string UnsupportedAssertionShape = nameof(UnsupportedAssertionShape);
    public const string UnsupportedWrite = nameof(UnsupportedWrite);
    public const string UnsupportedAlias = nameof(UnsupportedAlias);
    public const string UnsupportedFieldFlow = nameof(UnsupportedFieldFlow);
    public const string MissingDefinition = nameof(MissingDefinition);
    public const string MissingSource = nameof(MissingSource);
    public const string SemanticModelUnavailable = nameof(SemanticModelUnavailable);
    public const string ProjectLoadFailure = nameof(ProjectLoadFailure);
    public const string PathCapExceeded = nameof(PathCapExceeded);
    public const string NoAppliedTestArtifact = nameof(NoAppliedTestArtifact);
    public const string GeneratedTestMemberUnresolved = nameof(GeneratedTestMemberUnresolved);
    public const string PostAttemptAnalysisSkipped = nameof(PostAttemptAnalysisSkipped);
    public const string SemanticProjectUnavailable = nameof(SemanticProjectUnavailable);
    public const string SourceUnavailable = nameof(SourceUnavailable);
    public const string AnalysisDisabled = nameof(AnalysisDisabled);
    public const string AnalysisFailure = nameof(AnalysisFailure);
    public const string HistoricalNotMeasured = nameof(HistoricalNotMeasured);
}
