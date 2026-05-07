using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Bootstrap;

public interface ITestBootstrapService
{
    Task<TestBootstrapPlan> PlanAsync(CancellationToken cancellationToken = default);
    Task<TestBootstrapPlan> EnsureBootstrapAsync(CancellationToken cancellationToken = default);
}