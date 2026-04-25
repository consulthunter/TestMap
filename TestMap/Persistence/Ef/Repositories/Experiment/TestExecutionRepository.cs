using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;

namespace TestMap.Persistence.Ef.Repositories.Experiment;

public class TestExecutionRepository
{
    private readonly TestMapDbContext _context;

    public TestExecutionRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<TestExecution?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TestExecutions
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<TestExecution?> GetByAttemptIdAsync(int attemptId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TestExecutions
            .FirstOrDefaultAsync(t => t.GenerationAttemptId == attemptId, cancellationToken);
        return entity?.ToDomain();
    }

    public Task<TestExecution?> GetByAttemptAsync(int attemptId, CancellationToken cancellationToken = default)
    {
        return GetByAttemptIdAsync(attemptId, cancellationToken);
    }

    public async Task<List<TestExecution>> GetPassedExecutionsAsync(int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.TestExecutions
            .Where(t => t.GenerationAttempt != null &&
                        t.GenerationAttempt.CandidateMethod != null &&
                        t.GenerationAttempt.CandidateMethod.ExperimentRunId == experimentRunId)
            .Where(t => t.TestPassed)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<List<TestExecution>> GetExecutionsByClassificationAsync(
        int experimentRunId,
        TestClassification classification,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.TestExecutions
            .Where(t => t.GenerationAttempt != null &&
                        t.GenerationAttempt.CandidateMethod != null &&
                        t.GenerationAttempt.CandidateMethod.ExperimentRunId == experimentRunId)
            .Where(t => t.TestClassification == classification.ToString())
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<ExecutionStatistics> GetExecutionStatisticsAsync(int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.TestExecutions
            .Where(t => t.GenerationAttempt != null &&
                        t.GenerationAttempt.CandidateMethod != null &&
                        t.GenerationAttempt.CandidateMethod.ExperimentRunId == experimentRunId)
            .ToListAsync(cancellationToken);

        var executions = entities.Select(x => x.ToDomain()).ToList();

        var total = executions.Count;
        var passed = executions.Count(e => e.TestPassed);
        var compilationErrors = executions.Count(e => e.FailureKind == TestFailureKind.Compilation);
        var runtimeErrors = executions.Count(e =>
            e.FailureKind is TestFailureKind.Runtime or TestFailureKind.Assertion or TestFailureKind.Infrastructure);
        var coverageImprovements = executions.Count(e => e.Classification == TestClassification.Approved);

        return new ExecutionStatistics
        {
            TotalExecutions = total,
            PassedTests = passed,
            CompilationErrors = compilationErrors,
            RuntimeErrors = runtimeErrors,
            CoverageImprovements = coverageImprovements,
            AverageCoverage = executions.Any() ? executions.Average(e => e.CoverageAfter) : 0,
            AverageExecutionTimeMs = executions.Any() ? executions.Average(e => e.ExecutionTimeMs ?? 0) : 0,
            PassRate = total > 0 ? (double)passed / total : 0
        };
    }

    public async Task<Dictionary<TestClassification, int>> GetClassificationDistributionAsync(int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.TestExecutions
            .Where(t => t.GenerationAttempt != null &&
                        t.GenerationAttempt.CandidateMethod != null &&
                        t.GenerationAttempt.CandidateMethod.ExperimentRunId == experimentRunId)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain())
            .GroupBy(x => x.Classification)
            .ToDictionary(x => x.Key, x => x.Count());
    }

    public async Task<int> InsertAsync(TestExecution execution, CancellationToken cancellationToken = default)
    {
        var entity = execution.ToEntity();
        _context.TestExecutions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(TestExecution execution, CancellationToken cancellationToken = default)
    {
        var entity = execution.ToEntity();
        _context.TestExecutions.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TestExecutions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity != null)
        {
            _context.TestExecutions.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public class ExecutionStatistics
{
    public int TotalExecutions { get; set; }
    public int PassedTests { get; set; }
    public int CompilationErrors { get; set; }
    public int RuntimeErrors { get; set; }
    public int CoverageImprovements { get; set; }
    public double AverageCoverage { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double PassRate { get; set; }
}