using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public class EnvironmentSignalFlakinessFactorProvider : IFlakinessFactorProvider
{
    public FlakinessFactorKind Factor => FlakinessFactorKind.EnvironmentSignal;

    public Task<FlakinessFactorScore> GetScoreAsync(
        IReadOnlyList<TestExecutionResultModel> testHistory,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FlakinessFactorScore(Factor, 0.0,
            "Environment signal scoring is not implemented yet."));
    }
}