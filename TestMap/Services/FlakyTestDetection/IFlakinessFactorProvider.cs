using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public interface IFlakinessFactorProvider
{
    FlakinessFactorKind Factor { get; }

    Task<FlakinessFactorScore> GetScoreAsync(
        IReadOnlyList<TestExecutionResultModel> testHistory,
        CancellationToken cancellationToken = default);
}