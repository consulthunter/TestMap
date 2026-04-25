using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public class OutcomeVarianceFlakinessFactorProvider : IFlakinessFactorProvider
{
    public FlakinessFactorKind Factor => FlakinessFactorKind.OutcomeVariance;

    public Task<FlakinessFactorScore> GetScoreAsync(
        IReadOnlyList<TestExecutionResultModel> testHistory,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new FlakinessFactorScore(Factor, 0.0, "Outcome variance scoring is not implemented yet."));
    }
}