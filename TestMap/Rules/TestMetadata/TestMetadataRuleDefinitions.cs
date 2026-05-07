using TestMap.Models.Rules;

namespace TestMap.Rules.TestMetadata;

public static class TestMetadataRuleDefinitions
{
    private const string Version = "1.0";
    private const string Category = "TestMetadataEnrichment";

    public static RuleDefinition CategoryBenchmark { get; } = Define(
        "test-metadata.category.benchmark",
        "Benchmark category",
        "Benchmark attributes or BenchmarkRunner usage classify a test as a benchmark.");

    public static RuleDefinition CategoryParameterized { get; } = Define(
        "test-metadata.category.parameterized",
        "Parameterized test category",
        "Theory, TestCase, DataRow, or DataTestMethod attributes classify a test as parameterized.");

    public static RuleDefinition CategoryApiHost { get; } = Define(
        "test-metadata.category.api-host",
        "API host category",
        "HTTP client, TestServer, WebApplicationFactory, or API project signals classify a test as API and integration.");

    public static RuleDefinition CategoryBrowserAutomation { get; } = Define(
        "test-metadata.category.browser-automation",
        "Browser automation category",
        "Playwright, Selenium, or page navigation signals classify a test as UI and end-to-end.");

    public static RuleDefinition CategoryProperty { get; } = Define(
        "test-metadata.category.property",
        "Property test category",
        "FsCheck or property-testing signals classify a test as property based.");

    public static RuleDefinition CategorySmokeName { get; } = Define(
        "test-metadata.category.smoke-name",
        "Smoke test name category",
        "A test member name containing Smoke classifies the test as smoke.");

    public static RuleDefinition CategoryRegressionName { get; } = Define(
        "test-metadata.category.regression-name",
        "Regression test name category",
        "A test member name containing Regression classifies the test as regression.");

    public static RuleDefinition CategoryIntegrationResource { get; } = Define(
        "test-metadata.category.integration-resource",
        "Integration resource category",
        "Database, host, container, Docker, or filesystem signals classify a test as integration.");

    public static RuleDefinition CategoryUnitDefault { get; } = Define(
        "test-metadata.category.unit-default",
        "Unit test default category",
        "A test with no stronger deterministic signal defaults to UnitTest.");

    public static RuleDefinition IntentFallback { get; } = Define(
        "test-metadata.intent.fallback",
        "Fallback intent",
        "Fallback intent is derived from the test member name.");

    public static RuleDefinition IntentLlmNormalized { get; } = Define(
        "test-metadata.intent.llm-normalized",
        "LLM intent normalized",
        "A non-empty model intent is normalized into a sentence.");

    public static RuleDefinition SourceDeterministic { get; } = Define(
        "test-metadata.source.deterministic",
        "Deterministic metadata source",
        "Metadata was produced from deterministic rules only.");

    public static RuleDefinition SourceHybrid { get; } = Define(
        "test-metadata.source.hybrid",
        "Hybrid metadata source",
        "Metadata combines deterministic categories with model output.");

    public static RuleDefinition SourceLlm { get; } = Define(
        "test-metadata.source.llm",
        "LLM metadata source",
        "Metadata was produced from model output when no deterministic category was available.");

    public static RuleDefinition ConfidenceDeterministic { get; } = Define(
        "test-metadata.confidence.deterministic",
        "Deterministic confidence",
        "Deterministic confidence is selected from the number of deterministic categories.");

    public static RuleDefinition ConfidenceHybrid { get; } = Define(
        "test-metadata.confidence.hybrid",
        "Hybrid confidence",
        "Hybrid confidence is selected from model confidence or a category-count fallback.");

    public static RuleDefinition MergeAllowedLlmCategories { get; } = Define(
        "test-metadata.merge.allowed-llm-categories",
        "Merge allowed LLM categories",
        "Model categories are filtered to the allowed category set before merging.");

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        CategoryBenchmark,
        CategoryParameterized,
        CategoryApiHost,
        CategoryBrowserAutomation,
        CategoryProperty,
        CategorySmokeName,
        CategoryRegressionName,
        CategoryIntegrationResource,
        CategoryUnitDefault,
        IntentFallback,
        IntentLlmNormalized,
        SourceDeterministic,
        SourceHybrid,
        SourceLlm,
        ConfidenceDeterministic,
        ConfidenceHybrid,
        MergeAllowedLlmCategories
    ];

    private static RuleDefinition Define(string id, string name, string description)
    {
        return new RuleDefinition
        {
            Id = id,
            Version = Version,
            Name = name,
            Description = description,
            Category = Category
        };
    }
}
