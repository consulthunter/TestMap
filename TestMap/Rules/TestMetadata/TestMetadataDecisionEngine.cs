using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Services.StaticAnalysis.Enrichment;

namespace TestMap.Rules.TestMetadata;

internal static class TestMetadataDecisionEngine
{
    public static readonly string[] AllowedCategories =
    [
        "UnitTest",
        "IntegrationTest",
        "ApiTest",
        "UiTest",
        "Benchmark",
        "PropertyTest",
        "RegressionTest",
        "SmokeTest",
        "ParameterizedTest",
        "EndToEndTest"
    ];

    public static TestMetadataDecision InferDeterministicMetadata(TestMemberContext context)
    {
        var decisions = InferDeterministicCategoryDecisions(context);
        var categories = ExtractCategories(decisions);
        var intent = CreateFallbackIntent(context.Member.Name);
        decisions.Add(CreateDecision(
            "TestIntent",
            intent,
            TestMetadataRuleDefinitions.IntentFallback,
            RuleConfidence.Medium,
            [CreateEvidence("Member", "Name", context.Member.Name, context.TestFile.FilePath)],
            "Fallback test intent was derived from the member name."));

        var confidence = GetDeterministicConfidence(categories);
        decisions.Add(CreateDecision(
            "TestMetadataConfidence",
            confidence.ToString("0.##"),
            TestMetadataRuleDefinitions.ConfidenceDeterministic,
            RuleConfidence.Medium,
            [CreateEvidence("DeterministicCategories", "Count", categories.Count.ToString(), context.TestFile.FilePath)],
            "Deterministic confidence was selected from the number of inferred categories."));

        decisions.Add(CreateDecision(
            "TestMetadataSource",
            "Deterministic",
            TestMetadataRuleDefinitions.SourceDeterministic,
            RuleConfidence.High,
            [],
            "Metadata was produced by deterministic rules."));

        return new TestMetadataDecision(categories, intent, "Deterministic", confidence, string.Empty, decisions);
    }

    public static TestMetadataDecision ApplyLlmResult(
        TestMetadataDecision deterministicDecision,
        LlmMetadataResult llmResult,
        string promptVersion)
    {
        var decisions = deterministicDecision.RuleDecisions.ToList();
        var categories = MergeCategories(deterministicDecision.Categories, llmResult.Categories);
        decisions.Add(CreateDecision(
            "TestCategories",
            string.Join(";", categories),
            TestMetadataRuleDefinitions.MergeAllowedLlmCategories,
            RuleConfidence.Medium,
            llmResult.Categories.Select(category =>
                CreateEvidence("LlmResult", "Category", category, string.Empty)).ToList(),
            "Allowed model categories were merged with deterministic categories."));

        var intent = deterministicDecision.Intent;
        if (!string.IsNullOrWhiteSpace(llmResult.Intent))
        {
            intent = NormalizeIntent(llmResult.Intent);
            decisions.Add(CreateDecision(
                "TestIntent",
                intent,
                TestMetadataRuleDefinitions.IntentLlmNormalized,
                RuleConfidence.Medium,
                [CreateEvidence("LlmResult", "Intent", llmResult.Intent, string.Empty)],
                "Model intent was normalized into a sentence."));
        }

        var source = deterministicDecision.Categories.Count > 0 ? "Hybrid" : "Llm";
        decisions.Add(CreateDecision(
            "TestMetadataSource",
            source,
            source == "Hybrid" ? TestMetadataRuleDefinitions.SourceHybrid : TestMetadataRuleDefinitions.SourceLlm,
            RuleConfidence.Medium,
            [CreateEvidence("Prompt", "Version", promptVersion, string.Empty)],
            "Metadata source was selected after model enrichment."));

        var confidence = llmResult.Confidence ?? GetHybridConfidence(categories);
        decisions.Add(CreateDecision(
            "TestMetadataConfidence",
            confidence.ToString("0.##"),
            TestMetadataRuleDefinitions.ConfidenceHybrid,
            RuleConfidence.Medium,
            [CreateEvidence("LlmResult", "Confidence", llmResult.Confidence?.ToString("0.##") ?? string.Empty, string.Empty)],
            "Hybrid confidence used model confidence when available, otherwise a category-count fallback."));

        return new TestMetadataDecision(categories, intent, source, confidence, promptVersion, decisions);
    }

    public static List<RuleDecisionRecord> InferDeterministicCategoryDecisions(TestMemberContext context)
    {
        var decisions = new List<RuleDecisionRecord>();
        var attributes = context.Member.Attributes;
        var memberBody = context.Member.FullString;
        var objectBody = context.TestObject.FullString;
        var combinedText = string.Join(
            "\n",
            attributes,
            memberBody,
            objectBody,
            context.TestFile.FilePath,
            context.TestProject.FilePath,
            context.TestObject.TestFramework);

        if (attributes.Any(x => x.Contains("Benchmark", StringComparison.OrdinalIgnoreCase)) ||
            combinedText.Contains("BenchmarkRunner", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CategoryDecision(
                "Benchmark",
                TestMetadataRuleDefinitions.CategoryBenchmark,
                "Benchmark signal was found.",
                CreateEvidence("TestText", "Signal", "Benchmark", context.TestFile.FilePath)));

        if (attributes.Any(x => x.Contains("Theory", StringComparison.OrdinalIgnoreCase) ||
                                x.Contains("TestCase", StringComparison.OrdinalIgnoreCase) ||
                                x.Contains("DataRow", StringComparison.OrdinalIgnoreCase) ||
                                x.Contains("DataTestMethod", StringComparison.OrdinalIgnoreCase)))
            decisions.Add(CategoryDecision(
                "ParameterizedTest",
                TestMetadataRuleDefinitions.CategoryParameterized,
                "Parameterized test attribute signal was found.",
                CreateEvidence("Attributes", "Signal", string.Join(";", attributes), context.TestFile.FilePath)));

        if (combinedText.Contains("WebApplicationFactory", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("TestServer", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("HttpClient", StringComparison.OrdinalIgnoreCase) ||
            context.TestProject.FilePath.Contains(".Api", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CategoryDecision(
                "ApiTest;IntegrationTest",
                TestMetadataRuleDefinitions.CategoryApiHost,
                "API host signal was found.",
                CreateEvidence("TestText", "Signal", "API host or HTTP client", context.TestFile.FilePath)));

        if (combinedText.Contains("Playwright", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Selenium", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Page.GotoAsync", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CategoryDecision(
                "UiTest;EndToEndTest",
                TestMetadataRuleDefinitions.CategoryBrowserAutomation,
                "Browser automation signal was found.",
                CreateEvidence("TestText", "Signal", "Browser automation", context.TestFile.FilePath)));

        if (combinedText.Contains("FsCheck", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Property", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CategoryDecision(
                "PropertyTest",
                TestMetadataRuleDefinitions.CategoryProperty,
                "Property testing signal was found.",
                CreateEvidence("TestText", "Signal", "Property", context.TestFile.FilePath)));

        if (context.Member.Name.Contains("Smoke", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CategoryDecision(
                "SmokeTest",
                TestMetadataRuleDefinitions.CategorySmokeName,
                "Test member name contains Smoke.",
                CreateEvidence("Member", "Name", context.Member.Name, context.TestFile.FilePath)));

        if (context.Member.Name.Contains("Regression", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CategoryDecision(
                "RegressionTest",
                TestMetadataRuleDefinitions.CategoryRegressionName,
                "Test member name contains Regression.",
                CreateEvidence("Member", "Name", context.Member.Name, context.TestFile.FilePath)));

        if (combinedText.Contains("DbContext", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("SqlConnection", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("IHost", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Container", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Docker", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("File.", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Directory.", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CategoryDecision(
                "IntegrationTest",
                TestMetadataRuleDefinitions.CategoryIntegrationResource,
                "External resource signal was found.",
                CreateEvidence("TestText", "Signal", "External resource", context.TestFile.FilePath)));

        if (decisions.Count == 0)
            decisions.Add(CategoryDecision(
                "UnitTest",
                TestMetadataRuleDefinitions.CategoryUnitDefault,
                "No stronger deterministic test metadata signal was found.",
                CreateEvidence("Member", "Name", context.Member.Name, context.TestFile.FilePath)));

        return decisions;
    }

    public static List<string> MergeCategories(
        IReadOnlyCollection<string> deterministicCategories,
        IReadOnlyCollection<string> llmCategories)
    {
        var merged = new HashSet<string>(deterministicCategories, StringComparer.Ordinal);
        foreach (var category in llmCategories.Where(AllowedCategories.Contains)) merged.Add(category);

        if (merged.Count == 0) merged.Add("UnitTest");

        return merged.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public static double GetDeterministicConfidence(IReadOnlyCollection<string> categories)
    {
        return categories.Count switch
        {
            0 => 0.40,
            1 => 0.60,
            2 => 0.68,
            _ => 0.72
        };
    }

    public static double GetHybridConfidence(IReadOnlyCollection<string> categories)
    {
        return Math.Min(0.95, 0.72 + categories.Count * 0.05);
    }

    public static string CreateFallbackIntent(string memberName)
    {
        var readableName = string.IsNullOrWhiteSpace(memberName)
            ? "the target behavior"
            : memberName.Replace('_', ' ').Trim();

        return $"Tests {readableName}; ensures the expected behavior holds.";
    }

    public static string NormalizeIntent(string intent)
    {
        var trimmed = intent.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return string.Empty;

        if (!trimmed.EndsWith('.')) trimmed += ".";

        return trimmed;
    }

    private static List<string> ExtractCategories(IEnumerable<RuleDecisionRecord> decisions)
    {
        return decisions
            .Where(x => x.DecisionKind == "TestCategories")
            .SelectMany(x => x.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    private static RuleDecisionRecord CategoryDecision(
        string categories,
        RuleDefinition rule,
        string notes,
        RuleEvidenceRecord evidence)
    {
        return CreateDecision(
            "TestCategories",
            categories,
            rule,
            RuleConfidence.Medium,
            [evidence],
            notes);
    }

    private static RuleDecisionRecord CreateDecision(
        string decisionKind,
        string value,
        RuleDefinition rule,
        RuleConfidence confidence,
        List<RuleEvidenceRecord> evidence,
        string notes)
    {
        return RuleDecisionFactory.CreateDecision(
            decisionKind,
            value,
            rule,
            confidence,
            evidence,
            notes);
    }

    private static RuleEvidenceRecord CreateEvidence(string source, string key, string value, string location)
    {
        return RuleDecisionFactory.CreateEvidence(source, key, value, location);
    }
}

internal sealed record TestMetadataDecision(
    List<string> Categories,
    string Intent,
    string Source,
    double? Confidence,
    string PromptVersion,
    List<RuleDecisionRecord> RuleDecisions);

internal sealed class LlmMetadataResult
{
    public List<string> Categories { get; set; } = new();
    public string Intent { get; set; } = string.Empty;
    public double? Confidence { get; set; }
}
