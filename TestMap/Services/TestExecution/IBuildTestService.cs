using TestMap.Models.Results;

namespace TestMap.Services.TestExecution;

public interface IBuildTestService
{
    Task<TestRunModel> BuildTestAsync(BuildTestRunRequest request);
}

public enum BuildTestRunMode
{
    Baseline,
    Iteration
}

public sealed class BuildTestRunRequest
{
    public BuildTestRunMode Mode { get; init; }
    public List<string> Solutions { get; init; } = new();
    public string? TargetProjectPath { get; init; }
    public string? MutationSourceProjectPath { get; init; }
    public string? TargetFramework { get; init; }
    public string? CoveredMethodName { get; init; }

    public bool IsBaseline => Mode == BuildTestRunMode.Baseline;

    public static BuildTestRunRequest CreateBaseline(IEnumerable<string> solutions)
    {
        return new BuildTestRunRequest
        {
            Mode = BuildTestRunMode.Baseline,
            Solutions = solutions
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public static BuildTestRunRequest CreateIteration(
        string targetProjectPath,
        string? targetFramework,
        string? coveredMethodName,
        string? mutationSourceProjectPath = null)
    {
        return new BuildTestRunRequest
        {
            Mode = BuildTestRunMode.Iteration,
            TargetProjectPath = targetProjectPath,
            MutationSourceProjectPath =
                string.IsNullOrWhiteSpace(mutationSourceProjectPath) ? null : mutationSourceProjectPath,
            TargetFramework = string.IsNullOrWhiteSpace(targetFramework) ? null : targetFramework,
            CoveredMethodName = coveredMethodName
        };
    }
}