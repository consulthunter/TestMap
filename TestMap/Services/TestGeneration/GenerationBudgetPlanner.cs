using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Services.TestGeneration;

public static class GenerationBudgetPlanner
{
    public static IReadOnlyList<GenerationBudgetAttempt> Plan(GenerationBudgetMode budgetMode)
    {
        return budgetMode switch
        {
            GenerationBudgetMode.PassAt1 =>
            [
                new GenerationBudgetAttempt(1, false)
            ],
            GenerationBudgetMode.PassAt5 =>
            [
                new GenerationBudgetAttempt(1, false),
                new GenerationBudgetAttempt(2, false),
                new GenerationBudgetAttempt(3, false),
                new GenerationBudgetAttempt(4, false),
                new GenerationBudgetAttempt(5, false)
            ],
            GenerationBudgetMode.PassAt1RepairAt5 =>
            [
                new GenerationBudgetAttempt(1, false),
                new GenerationBudgetAttempt(2, true),
                new GenerationBudgetAttempt(3, true),
                new GenerationBudgetAttempt(4, true),
                new GenerationBudgetAttempt(5, true)
            ],
            _ => throw new InvalidOperationException($"Unsupported generation budget mode: {budgetMode}")
        };
    }
}

public sealed record GenerationBudgetAttempt(int AttemptNumber, bool IsRepair);
