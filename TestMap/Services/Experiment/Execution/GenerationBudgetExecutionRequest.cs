using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;

namespace TestMap.Services.Experiment.Execution;

public sealed class GenerationBudgetExecutionRequest
{
    public required GenerationBudgetMode BudgetMode { get; init; }
    public int PassAtCount { get; init; } = 5;
    public int RepairAttemptCount { get; init; } = 5;
    public required Func<int, CancellationToken, Task<GenerationAttempt>> GenerateAsync { get; init; }
    public Func<GenerationAttempt, int, CancellationToken, Task<GenerationAttempt>>? RepairAsync { get; init; }
    public Func<GenerationAttempt, bool>? ShouldStopRepair { get; init; }
    public Func<CancellationToken, Task>? RollbackAsync { get; init; }
}
