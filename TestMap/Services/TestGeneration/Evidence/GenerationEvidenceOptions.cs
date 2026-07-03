using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Evidence;

public sealed class GenerationEvidenceOptions
{
    public required CandidateMethodContext CandidateContext { get; init; }
    public TestGenerationObjective Objective { get; init; } = TestGenerationObjective.TestSuiteExpansion;
    public TestGenerationApproach Approach { get; init; } = TestGenerationApproach.MetricsDriven;
    public MetricsDrivenPath? MetricsPath { get; init; }
}
