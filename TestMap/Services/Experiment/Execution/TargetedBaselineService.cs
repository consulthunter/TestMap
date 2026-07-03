using TestMap.Models.Experiment;
using TestMap.Services.TestExecution;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.Experiment.Execution;

public sealed class TargetedBaselineService : ITargetedBaselineService
{
    private readonly IBuildTestService _buildTestService;

    public TargetedBaselineService(IBuildTestService buildTestService)
    {
        _buildTestService = buildTestService;
    }

    public async Task<TargetedBaselineResult> RunAsync(
        int experimentRunId,
        CandidateMethod candidate,
        CandidateMethodContext methodContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(methodContext.TestProjectPath))
            return TargetedBaselineResultSkipped("Candidate has no resolved test project path.");

        if (string.IsNullOrWhiteSpace(methodContext.SourceProjectPath))
            return TargetedBaselineResultSkipped("Candidate has no resolved source project path.");

        var run = await _buildTestService.BuildTestAsync(
            BuildTestRunRequest.CreateIteration(
                methodContext.TestProjectPath,
                methodContext.TargetBuildFramework,
                candidate.MethodName,
                methodContext.SourceProjectPath,
                experimentRunId,
                isMutationBaseline: true));

        int? testRunId = run.DbId > 0 ? run.DbId : null;
        return new TargetedBaselineResult
        {
            TestRunId = testRunId,
            Ran = true,
            SkipReason = testRunId.HasValue ? string.Empty : "Baseline ran but did not return a persisted test run id."
        };
    }

    private static TargetedBaselineResult TargetedBaselineResultSkipped(string reason)
    {
        return new TargetedBaselineResult
        {
            Ran = false,
            SkipReason = reason
        };
    }
}
