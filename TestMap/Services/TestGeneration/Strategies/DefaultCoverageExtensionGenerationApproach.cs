using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Strategies;

public sealed class DefaultCoverageExtensionGenerationApproach : ITestGenerationApproach
{
    public TestGenerationApproach Strategy => TestGenerationApproach.DefaultCoverageExtension;

    public bool ShouldSkipGeneration(CandidateMethodContext context)
    {
        return false;
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
            ExampleTestMetadataSummary = methodContext.ExampleTestMetadataSummary,
            ProjectTestMetadataSummary = methodContext.ProjectTestMetadataSummary,
            TestClass = methodContext.TestClass,
            TestFileContents = methodContext.TestFileContents,
            TestSupportContext = methodContext.TestSupportContext,
            TestFramework = methodContext.TestFramework,
            TestDependencies = methodContext.TestDependencies,
            CoverageGapSummary = methodContext.CoverageGapSummary,
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
            ExampleTestMetadataSummary = methodContext.ExampleTestMetadataSummary,
            ProjectTestMetadataSummary = methodContext.ProjectTestMetadataSummary,
            CoverageGapSummary = methodContext.CoverageGapSummary,
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
}