using TestMap.App;
using TestMap.Models.Results;
using TestMap.Persistence.Ef.Repositories.MutationTesting;

namespace TestMap.Services.TestExecution.Mapping;

public class MapMutationService(
    ProjectContext context,
    MutationTestingReportRepository mutationTestingReportRepository)
{
    public async Task<double> MapAsync(StrykerMutationResults report, int? testRunId = null)
    {
        if (context.Project.DbId == 0)
        {
            context.Project.Logger?.Warning("Skipping mutation mapping because the project database ID is not set.");
            return 0.0;
        }

        var score = CalculateMutationScore(report);
        await mutationTestingReportRepository.InsertOrUpdateAsync(report, context.Project.DbId, testRunId, score);
        return score;
    }

    public double CalculateMutationScore(StrykerMutationResults report)
    {
        var aggregate = new StrykerMutationScoreResult();

        if (report.files == null || report.files.Count == 0) return 0.0;

        foreach (var file in report.files.Values)
        {
            if (file?.mutants == null) continue;

            aggregate = Merge(aggregate, CalculateFileScore(file));
        }

        return aggregate.Score * 100;
    }

    private static StrykerMutationScoreResult Merge(StrykerMutationScoreResult left, StrykerMutationScoreResult right)
    {
        return left with
        {
            Killed = left.Killed + right.Killed,
            Survived = left.Survived + right.Survived,
            Timeout = left.Timeout + right.Timeout,
            NoCoverage = left.NoCoverage + right.NoCoverage,
            Ignored = left.Ignored + right.Ignored,
            CompileErrors = left.CompileErrors + right.CompileErrors
        };
    }

    private static StrykerMutationScoreResult CalculateFileScore(StrykerFileResult fileResult)
    {
        var result = new StrykerMutationScoreResult();

        foreach (var mutant in fileResult.mutants)
        {
            var status = mutant.status?.ToLowerInvariant();

            if (status == "ignored")
            {
                result = result with { Ignored = result.Ignored + 1 };
                continue;
            }

            if (mutant.@static) continue;

            switch (status)
            {
                case "killed":
                    result = result with { Killed = result.Killed + 1 };
                    break;
                case "survived":
                    result = result with { Survived = result.Survived + 1 };
                    break;
                case "timeout":
                    result = result with { Timeout = result.Timeout + 1 };
                    break;
                case "nocoverage":
                    result = result with { NoCoverage = result.NoCoverage + 1 };
                    break;
                case "compileerrors":
                    result = result with { CompileErrors = result.CompileErrors + 1 };
                    break;
            }
        }

        return result;
    }
}
