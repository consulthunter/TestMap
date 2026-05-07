using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Rules;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Evidence;

public sealed class GenerationEvidencePackage
{
    public TestGenerationObjective Objective { get; init; } = TestGenerationObjective.TestSuiteExpansion;
    public TestGenerationApproach Approach { get; init; } = TestGenerationApproach.MetricsDriven;
    public MetricsDrivenPath? MetricsPath { get; init; }
    public CandidateMethodContext CandidateContext { get; init; } = default!;
    public string StrategyInstruction { get; init; } = string.Empty;
    public CoverageEvidence? Coverage { get; init; }
    public MutationEvidence? Mutation { get; init; }
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}

public sealed class CoverageEvidence
{
    public IReadOnlyList<CoverageGapEvidence> Gaps { get; init; } = [];
    public double? CurrentLineCoverage { get; init; }
    public double? CurrentBranchCoverage { get; init; }
    public string CoverageReportId { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
}

public sealed class CoverageGapEvidence
{
    public int LineNumber { get; init; }
    public string GapKind { get; init; } = string.Empty;
    public int Hits { get; init; }
    public string ConditionCoverage { get; init; } = string.Empty;
    public string SourceText { get; init; } = string.Empty;
}

public sealed class MutationEvidence
{
    public IReadOnlyList<SurvivingMutantEvidence> SurvivingMutants { get; init; } = [];
    public double? BaselineMutationScore { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public sealed class SurvivingMutantEvidence
{
    public string MutantId { get; init; } = string.Empty;
    public string MutatorName { get; init; } = string.Empty;
    public string OriginalCode { get; init; } = string.Empty;
    public string ReplacementCode { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusReason { get; init; } = string.Empty;
    public IReadOnlyList<string> CoveringTests { get; init; } = [];
}
