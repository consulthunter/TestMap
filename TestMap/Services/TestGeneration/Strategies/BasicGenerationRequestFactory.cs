using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Services.TestGeneration.Strategies;

internal static class BasicGenerationRequestFactory
{
    public static TestGenerationRequest CreateGenerationRequest(
        TestGenerationApproachContext context,
        TestGenerationApproach approach,
        string coverageGapSummary)
    {
        var methodContext = context.MethodContext;

        return new TestGenerationRequest
        {
            Approach = approach,
            MethodBody = methodContext.Method.SourceCode,
            MethodName = methodContext.Method.MethodName,
            MethodSignature = methodContext.MethodSignature,
            ContainingClass = methodContext.ContainingClass,
            SourceFilePath = methodContext.SourceFilePath,
            SourceProjectPath = methodContext.SourceProjectPath,
            SolutionFilePath = methodContext.SolutionFilePath,
            SourceStartLine = methodContext.SourceLocation.StartLine,
            SourceEndLine = methodContext.SourceLocation.EndLine,
            SourceStartPosition = methodContext.SourceLocation.StartPosition,
            SourceEndPosition = methodContext.SourceLocation.EndPosition,
            ExistingTestFilePath = methodContext.TestLocation?.TestFilePath,
            ExistingTestStartLine = methodContext.TestLocation?.StartLine,
            ExistingTestEndLine = methodContext.TestLocation?.EndLine,
            ExampleTest = methodContext.ExampleTest,
            ExampleTestMetadataSummary = methodContext.ExampleTestMetadataSummary,
            ProjectTestMetadataSummary = methodContext.ProjectTestMetadataSummary,
            TestClass = methodContext.TestClass,
            TestFileContents = methodContext.TestFileContents,
            TestSupportContext = methodContext.TestSupportContext,
            TestFramework = methodContext.TestFramework,
            TestDependencies = methodContext.TestDependencies,
            CoverageGapSummary = coverageGapSummary,
            Provider = context.Provider,
            Temperature = context.Temperature,
            StepErrorRetries = context.StepErrorRetries,
            StepRetryDelayMs = context.StepRetryDelayMs
        };
    }

    public static TestRepairRequest CreateRepairRequest(
        TestRepairApproachContext context,
        TestGenerationApproach approach,
        string coverageGapSummary)
    {
        var methodContext = context.MethodContext;

        return new TestRepairRequest
        {
            Approach = approach,
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
            CoverageGapSummary = coverageGapSummary,
            ErrorLogs = context.ErrorLogs,
            StructuredErrors = context.StructuredErrors,
            PriorConversationTranscript = context.PriorConversationTranscript,
            Provider = context.Provider,
            Temperature = context.Temperature,
            AttemptNumber = context.AttemptNumber,
            StepErrorRetries = context.StepErrorRetries,
            StepRetryDelayMs = context.StepRetryDelayMs
        };
    }
}
