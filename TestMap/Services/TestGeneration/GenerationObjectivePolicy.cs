using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;

namespace TestMap.Services.TestGeneration;

public static class GenerationObjectivePolicy
{
    public static TestActionExecutorMode ResolveExecutor(TestGenerationObjective objective)
    {
        return objective switch
        {
            TestGenerationObjective.TestSuiteExpansion => TestActionExecutorMode.BasicExtension,
            _ => throw new InvalidOperationException($"Unsupported test generation objective '{objective}'.")
        };
    }

    public static IReadOnlySet<CandidateActionKind> GetAllowedActions(TestGenerationObjective objective)
    {
        return objective switch
        {
            TestGenerationObjective.TestSuiteExpansion => new HashSet<CandidateActionKind>
            {
                CandidateActionKind.GenerateNewTest,
                CandidateActionKind.ExtendExistingTestSuite
            },
            _ => throw new InvalidOperationException($"Unsupported test generation objective '{objective}'.")
        };
    }

    public static bool IsApproachSupported(
        TestGenerationObjective objective,
        TestGenerationApproach approach)
    {
        return objective switch
        {
            TestGenerationObjective.TestSuiteExpansion => approach is
                TestGenerationApproach.Naive or
                TestGenerationApproach.MetricsDriven,
            _ => false
        };
    }

    public static void Validate(
        TestGenerationObjective objective,
        IEnumerable<TestGenerationApproach> approaches,
        TestActionExecutorMode configuredExecutor)
    {
        var resolvedExecutor = ResolveExecutor(objective);
        if (configuredExecutor != resolvedExecutor)
            throw new InvalidOperationException(
                $"Objective '{objective}' requires executor '{resolvedExecutor}'. Configured executor '{configuredExecutor}' is reserved for another objective.");

        foreach (var approach in approaches)
        {
            if (!IsApproachSupported(objective, approach))
                throw new InvalidOperationException(
                    $"Generation approach '{approach}' is not supported for objective '{objective}'.");
        }
    }
}
