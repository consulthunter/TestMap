using TestMap.Models.Experiment;
using TestMap.Services.TestExecution;
using TestMap.Services.TestGeneration.Execution;
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
                methodContext.ToTargetMemberDescriptor(candidate),
                methodContext.SourceProjectPath,
                experimentRunId,
                isMutationBaseline: true));

        int? testRunId = run.DbId > 0 ? run.DbId : null;
        var usableRun = GeneratedTestExecutionService.IsUsableValidationRun(run);
        return new TargetedBaselineResult
        {
            TestRunId = testRunId,
            Ran = true,
            Usable = testRunId.HasValue && usableRun,
            SkipReason = !testRunId.HasValue
                ? "Baseline ran but did not return a persisted test run id."
                : usableRun
                    ? string.Empty
                    : run.FailureAnalysis?.Summary ?? "Baseline did not compile or execute any tests."
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
