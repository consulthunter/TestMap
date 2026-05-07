using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Strategies;

public sealed class MetricsDrivenGenerationApproach : ITestGenerationApproach
{
    public TestGenerationApproach Strategy => TestGenerationApproach.MetricsDriven;

    public bool ShouldSkipGeneration(CandidateMethodContext context)
    {
        return false;
    }

    public TestGenerationRequest CreateGenerationRequest(TestGenerationApproachContext context)
    {
        return BasicGenerationRequestFactory.CreateGenerationRequest(
            context,
            TestGenerationApproach.MetricsDriven,
            context.MethodContext.CoverageGapSummary);
    }

    public TestRepairRequest CreateRepairRequest(TestRepairApproachContext context)
    {
        return BasicGenerationRequestFactory.CreateRepairRequest(
            context,
            TestGenerationApproach.MetricsDriven,
            context.MethodContext.CoverageGapSummary);
    }
}
