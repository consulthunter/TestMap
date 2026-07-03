using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public class FailureSignatureFlakinessFactorProvider : IFlakinessFactorProvider
{
    public FlakinessFactorKind Factor => FlakinessFactorKind.FailureSignature;

    public Task<FlakinessFactorScore> GetScoreAsync(
        IReadOnlyList<TestExecutionResultModel> testHistory,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FlakinessFactorScore(Factor, 0.0,
            "Failure signature scoring is not implemented yet."));
    }
}