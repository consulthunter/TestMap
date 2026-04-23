using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public class DurationVarianceFlakinessFactorProvider : IFlakinessFactorProvider
{
    public FlakinessFactorKind Factor => FlakinessFactorKind.DurationVariance;

    public Task<FlakinessFactorScore> GetScoreAsync(
        IReadOnlyList<TestExecutionResultModel> testHistory,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FlakinessFactorScore(Factor, 0.0, "Duration variance scoring is not implemented yet."));
    }
}
