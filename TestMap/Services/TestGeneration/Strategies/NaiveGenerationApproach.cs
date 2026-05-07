using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Strategies;

public sealed class NaiveGenerationApproach : ITestGenerationApproach
{
    public TestGenerationApproach Strategy => TestGenerationApproach.Naive;

    public bool ShouldSkipGeneration(CandidateMethodContext context)
    {
        return false;
    }

    public TestGenerationRequest CreateGenerationRequest(TestGenerationApproachContext context)
    {
        return BasicGenerationRequestFactory.CreateGenerationRequest(
            context,
            TestGenerationApproach.Naive,
            coverageGapSummary: string.Empty);
    }

    public TestRepairRequest CreateRepairRequest(TestRepairApproachContext context)
    {
        return BasicGenerationRequestFactory.CreateRepairRequest(
            context,
            TestGenerationApproach.Naive,
            coverageGapSummary: string.Empty);
    }
}
