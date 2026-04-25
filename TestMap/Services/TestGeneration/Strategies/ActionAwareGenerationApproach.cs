using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Strategies;

public sealed class ActionAwareGenerationApproach : ITestGenerationApproach
{
    public TestGenerationApproach Strategy => TestGenerationApproach.ActionAware;

    public bool ShouldSkipGeneration(CandidateMethodContext context)
    {
        return context.Method.RecommendedAction == CandidateActionKind.Skip;
    }

    public TestGenerationRequest CreateGenerationRequest(TestGenerationApproachContext context)
    {
        var methodContext = context.MethodContext;

        return new TestGenerationRequest
        {
            MethodBody = methodContext.Method.SourceCode,
            MethodName = methodContext.Method.MethodName,
            MethodSignature = methodContext.MethodSignature,
            ContainingClass = methodContext.ContainingClass,
            ExampleTest = methodContext.ExampleTest,
            ExampleTestMetadataSummary = CombineMetadata(
                methodContext.ExampleTestMetadataSummary,
                BuildActionGuidance(methodContext)),
            ProjectTestMetadataSummary = methodContext.ProjectTestMetadataSummary,
            TestClass = methodContext.TestClass,
            TestFileContents = methodContext.TestFileContents,
            TestSupportContext = methodContext.TestSupportContext,
            TestFramework = methodContext.TestFramework,
            TestDependencies = methodContext.TestDependencies,
            CoverageGapSummary = CombineMetadata(
                methodContext.CoverageGapSummary,
                BuildCoverageDirective(methodContext)),
            Provider = context.Provider,
            Temperature = context.Temperature,
            StepErrorRetries = context.StepErrorRetries,
            StepRetryDelayMs = context.StepRetryDelayMs,
            EnableHistoryChaining = context.EnableHistoryChaining
        };
    }

    public TestRepairRequest CreateRepairRequest(TestRepairApproachContext context)
    {
        var methodContext = context.MethodContext;

        return new TestRepairRequest
        {
            MethodBody = methodContext.Method.SourceCode,
            MethodName = methodContext.Method.MethodName,
            GeneratedTest = context.GeneratedTest,
            TestClass = methodContext.TestClass,
            TestFramework = methodContext.TestFramework,
            TestDependencies = methodContext.TestDependencies,
            TestFileContents = methodContext.TestFileContents,
            TestSupportContext = methodContext.TestSupportContext,
            ExampleTestMetadataSummary = CombineMetadata(
                methodContext.ExampleTestMetadataSummary,
                BuildActionGuidance(methodContext)),
            ProjectTestMetadataSummary = methodContext.ProjectTestMetadataSummary,
            CoverageGapSummary = CombineMetadata(
                methodContext.CoverageGapSummary,
                BuildRepairDirective(methodContext)),
            ErrorLogs = context.ErrorLogs,
            StructuredErrors = context.StructuredErrors,
            PriorConversationTranscript = context.PriorConversationTranscript,
            Provider = context.Provider,
            Temperature = context.Temperature,
            AttemptNumber = context.AttemptNumber,
            StepErrorRetries = context.StepErrorRetries,
            StepRetryDelayMs = context.StepRetryDelayMs,
            EnableHistoryChaining = context.EnableHistoryChaining
        };
    }

    private static string BuildActionGuidance(CandidateMethodContext context)
    {
        return context.Method.RecommendedAction switch
        {
            CandidateActionKind.GenerateNewTest =>
                "Generation intent: create a new test for this source method because no reliable baseline test is known.",
            CandidateActionKind.ImproveExistingTest =>
                "Generation intent: improve an existing test for this source method by strengthening assertions, reducing ambiguity, and preserving valid coverage.",
            CandidateActionKind.ExtendExistingTestSuite =>
                "Generation intent: add a new test case to extend the existing suite with missing scenarios or uncovered branches.",
            CandidateActionKind.Skip =>
                "Generation intent: skip unless execution explicitly overrides this recommendation.",
            _ =>
                "Generation intent: add a focused test that improves coverage for the source method."
        };
    }

    private static string BuildCoverageDirective(CandidateMethodContext context)
    {
        return context.Method.RecommendedAction switch
        {
            CandidateActionKind.ImproveExistingTest =>
                "Prefer improving the quality of the existing test path instead of inventing unrelated scenarios.",
            CandidateActionKind.ExtendExistingTestSuite =>
                "Prefer a new scenario that complements the existing suite and targets missing behavior or edge cases.",
            _ =>
                "Prefer a focused new test that increases meaningful coverage."
        };
    }

    private static string BuildRepairDirective(CandidateMethodContext context)
    {
        return context.Method.RecommendedAction switch
        {
            CandidateActionKind.ImproveExistingTest =>
                "Repair with emphasis on preserving the existing test intent while improving correctness and assertion quality.",
            CandidateActionKind.ExtendExistingTestSuite =>
                "Repair with emphasis on keeping the new case additive to the current suite.",
            _ =>
                "Repair with emphasis on producing a valid focused test that improves coverage."
        };
    }

    private static string CombineMetadata(string primary, string? supplemental)
    {
        if (string.IsNullOrWhiteSpace(supplemental)) return primary;

        return string.IsNullOrWhiteSpace(primary)
            ? supplemental
            : $"{primary}{Environment.NewLine}{Environment.NewLine}{supplemental}";
    }
}