using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.Experiment.Execution;
using ExperimentTestExecution = TestMap.Models.Experiment.TestExecution;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GenerationBudgetExecutorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_PassAt5_RunsFiveIndependentAttemptsAndRollsBackEach()
    {
        var executor = new GenerationBudgetExecutor();
        var generatedAttempts = 0;
        var rollbacks = 0;

        var result = await executor.ExecuteAsync(new GenerationBudgetExecutionRequest
        {
            BudgetMode = GenerationBudgetMode.PassAt5,
            GenerateAsync = (attemptNumber, _) =>
            {
                generatedAttempts++;
                return Task.FromResult(new GenerationAttempt { AttemptNumber = attemptNumber });
            },
            RollbackAsync = _ =>
            {
                rollbacks++;
                return Task.CompletedTask;
            }
        });

        Assert.Equal(5, generatedAttempts);
        Assert.Equal(5, rollbacks);
        Assert.Equal([1, 2, 3, 4, 5], result.Select(x => x.AttemptNumber).ToArray());
        Assert.All(result, x => Assert.False(x.IsRepairAttempt));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_RepairBudget_RepairsUntilSuccess()
    {
        var executor = new GenerationBudgetExecutor();

        var result = await executor.ExecuteAsync(new GenerationBudgetExecutionRequest
        {
            BudgetMode = GenerationBudgetMode.PassAt1RepairAt5,
            GenerateAsync = (attemptNumber, _) => Task.FromResult(new GenerationAttempt
            {
                AttemptNumber = attemptNumber,
                TestExecution = new ExperimentTestExecution
                {
                    TestPassed = false,
                    CoverageImprovement = 0
                }
            }),
            RepairAsync = (previousAttempt, attemptNumber, _) => Task.FromResult(new GenerationAttempt
            {
                AttemptNumber = attemptNumber,
                TestExecution = new ExperimentTestExecution
                {
                    TestPassed = attemptNumber == 3,
                    CoverageImprovement = attemptNumber == 3 ? 0.1 : 0
                }
            }),
            ShouldStopRepair = attempt => attempt.TestExecution is { TestPassed: true, CoverageImprovement: > 0 }
        });

        Assert.Equal(3, result.Count);
        Assert.False(result[0].IsRepairAttempt);
        Assert.True(result[1].IsRepairAttempt);
        Assert.True(result[2].IsRepairAttempt);
        Assert.Equal(1, result[1].ParentAttemptNumber);
        Assert.Equal(2, result[2].ParentAttemptNumber);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_PassAt_RollsBackWhenGenerationThrows()
    {
        var executor = new GenerationBudgetExecutor();
        var rollbacks = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(new GenerationBudgetExecutionRequest
            {
                BudgetMode = GenerationBudgetMode.PassAt1,
                GenerateAsync = (_, _) => throw new InvalidOperationException("generation failed"),
                RollbackAsync = _ =>
                {
                    rollbacks++;
                    return Task.CompletedTask;
                }
            }));

        Assert.Equal(1, rollbacks);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_RepairBudget_RollsBackWhenRepairThrows()
    {
        var executor = new GenerationBudgetExecutor();
        var rollbacks = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(new GenerationBudgetExecutionRequest
            {
                BudgetMode = GenerationBudgetMode.PassAt1RepairAt5,
                GenerateAsync = (attemptNumber, _) => Task.FromResult(new GenerationAttempt
                {
                    AttemptNumber = attemptNumber,
                    TestExecution = new ExperimentTestExecution
                    {
                        TestPassed = false,
                        CoverageImprovement = 0
                    }
                }),
                RepairAsync = (_, _, _) => throw new InvalidOperationException("repair failed"),
                RollbackAsync = _ =>
                {
                    rollbacks++;
                    return Task.CompletedTask;
                }
            }));

        Assert.Equal(1, rollbacks);
    }
}
