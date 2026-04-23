using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public class RerunInstabilityFlakinessFactorProvider : IFlakinessFactorProvider
{
    public FlakinessFactorKind Factor => FlakinessFactorKind.RerunInstability;

    public Task<FlakinessFactorScore> GetScoreAsync(
        IReadOnlyList<TestExecutionResultModel> testHistory,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FlakinessFactorScore(Factor, 0.0, "Rerun instability scoring is not implemented yet."));
    }
}
