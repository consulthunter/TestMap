using TestMap.Models.Rules;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Rules.TestMetadata;

namespace TestMap.Services.StaticAnalysis.Enrichment;

internal static class TestMetadataClassifier
{
    public static readonly string[] AllowedCategories = TestMetadataDecisionEngine.AllowedCategories;

    public static TestMetadataDecision InferDeterministicMetadata(TestMemberContext context)
    {
        return TestMetadataDecisionEngine.InferDeterministicMetadata(context);
    }

    public static TestMetadataDecision ApplyLlmResult(
        TestMetadataDecision deterministicDecision,
        LlmMetadataResult llmResult,
        string promptVersion)
    {
        return TestMetadataDecisionEngine.ApplyLlmResult(deterministicDecision, llmResult, promptVersion);
    }

    public static List<RuleDecisionRecord> InferDeterministicCategoryDecisions(TestMemberContext context)
    {
        return TestMetadataDecisionEngine.InferDeterministicCategoryDecisions(context);
    }

    public static List<string> InferDeterministicCategories(TestMemberContext context)
    {
        return InferDeterministicMetadata(context).Categories;
    }

    public static List<string> MergeCategories(
        IReadOnlyCollection<string> deterministicCategories,
        IReadOnlyCollection<string> llmCategories)
    {
        return TestMetadataDecisionEngine.MergeCategories(deterministicCategories, llmCategories);
    }

    public static double GetDeterministicConfidence(IReadOnlyCollection<string> categories)
    {
        return TestMetadataDecisionEngine.GetDeterministicConfidence(categories);
    }

    public static double GetHybridConfidence(IReadOnlyCollection<string> categories)
    {
        return TestMetadataDecisionEngine.GetHybridConfidence(categories);
    }

    public static string CreateFallbackIntent(string memberName)
    {
        return TestMetadataDecisionEngine.CreateFallbackIntent(memberName);
    }

    public static string NormalizeIntent(string intent)
    {
        return TestMetadataDecisionEngine.NormalizeIntent(intent);
    }
}

internal sealed class TestMemberContext
{
    public required MemberEntity Member { get; init; }
    public required ObjectEntity TestObject { get; init; }
    public required FileEntity TestFile { get; init; }
    public required CSharpProjectEntity TestProject { get; init; }
}
