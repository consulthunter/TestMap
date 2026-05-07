using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Services.Experiment.Execution;

public sealed class GenerationBudgetExecutor : IGenerationBudgetExecutor
{
    public async Task<IReadOnlyList<GeneratedCandidateEvaluation>> ExecuteAsync(
        GenerationBudgetExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        return request.BudgetMode switch
        {
            GenerationBudgetMode.PassAt1 => await ExecutePassAtAsync(request, 1, cancellationToken),
            GenerationBudgetMode.PassAt5 => await ExecutePassAtAsync(request, request.PassAtCount, cancellationToken),
            GenerationBudgetMode.PassAt1RepairAt5 => await ExecuteRepairAsync(request, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported budget mode: {request.BudgetMode}")
        };
    }

    private static async Task<IReadOnlyList<GeneratedCandidateEvaluation>> ExecutePassAtAsync(
        GenerationBudgetExecutionRequest request,
        int count,
        CancellationToken cancellationToken)
    {
        var evaluations = new List<GeneratedCandidateEvaluation>();

        for (var attemptNumber = 1; attemptNumber <= count; attemptNumber++)
        {
            try
            {
                var attempt = await request.GenerateAsync(attemptNumber, cancellationToken);
                evaluations.Add(new GeneratedCandidateEvaluation
                {
                    Attempt = attempt,
                    AttemptNumber = attemptNumber
                });
            }
            finally
            {
                if (request.RollbackAsync != null)
                    await request.RollbackAsync(cancellationToken);
            }
        }

        return evaluations;
    }

    private static async Task<IReadOnlyList<GeneratedCandidateEvaluation>> ExecuteRepairAsync(
        GenerationBudgetExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RepairAsync == null)
            throw new InvalidOperationException("Repair budget execution requires a repair callback.");

        try
        {
            var evaluations = new List<GeneratedCandidateEvaluation>();
            var current = await request.GenerateAsync(1, cancellationToken);
            evaluations.Add(new GeneratedCandidateEvaluation
            {
                Attempt = current,
                AttemptNumber = 1
            });

            if (request.ShouldStopRepair?.Invoke(current) == true)
                return evaluations;

            for (var attemptNumber = 2; attemptNumber <= request.RepairAttemptCount; attemptNumber++)
            {
                var repaired = await request.RepairAsync(current, attemptNumber, cancellationToken);
                evaluations.Add(new GeneratedCandidateEvaluation
                {
                    Attempt = repaired,
                    AttemptNumber = attemptNumber,
                    ParentAttemptNumber = current.AttemptNumber,
                    IsRepairAttempt = true
                });

                current = repaired;

                if (request.ShouldStopRepair?.Invoke(current) == true)
                    break;
            }

            return evaluations;
        }
        finally
        {
            if (request.RollbackAsync != null)
                await request.RollbackAsync(cancellationToken);
        }
    }
}
