using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Services.StaticAnalysis.Enrichment;
using TestMap.Rules.TestMetadata;

namespace TestMap.UnitTests.StaticAnalysis.Enrichment;

public sealed class TestMetadataClassifierTests
{
    /// <summary>
    /// Verifies that deterministic classification falls back to UnitTest when no stronger signal is present.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void InferDeterministicCategories_WithNoSpecificSignals_ReturnsUnitTest()
    {
        // Arrange
        var context = CreateContext(memberName: "Adds_numbers");

        // Act
        var categories = TestMetadataClassifier.InferDeterministicCategories(context);

        // Assert
        Assert.Equal(["UnitTest"], categories);
        var decision = Assert.Single(TestMetadataClassifier.InferDeterministicCategoryDecisions(context));
        Assert.Equal("test-metadata.category.unit-default", decision.RuleId);
        Assert.Equal("1.0", decision.RuleVersion);
    }

    /// <summary>
    /// Verifies that xUnit theory-style attributes are classified as parameterized tests.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void InferDeterministicCategories_WithTheoryAttribute_ReturnsParameterizedTest()
    {
        // Arrange
        var context = CreateContext(attributes: ["[Theory]", "[InlineData(1, 2, 3)]"]);

        // Act
        var categories = TestMetadataClassifier.InferDeterministicCategories(context);

        // Assert
        Assert.Equal(["ParameterizedTest"], categories);
        var decision = Assert.Single(TestMetadataClassifier.InferDeterministicCategoryDecisions(context));
        Assert.Equal("test-metadata.category.parameterized", decision.RuleId);
    }

    /// <summary>
    /// Verifies that HTTP test host signals are classified as both API and integration tests.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void InferDeterministicCategories_WithApiHostSignal_ReturnsApiAndIntegrationTests()
    {
        // Arrange
        var context = CreateContext(memberBody: "using var client = factory.CreateClient(); HttpClient client;");

        // Act
        var categories = TestMetadataClassifier.InferDeterministicCategories(context);

        // Assert
        Assert.Equal(["ApiTest", "IntegrationTest"], categories);
        var decision = Assert.Single(TestMetadataClassifier.InferDeterministicCategoryDecisions(context));
        Assert.Equal("test-metadata.category.api-host", decision.RuleId);
    }

    /// <summary>
    /// Verifies that browser automation signals are classified as end-to-end UI tests.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void InferDeterministicCategories_WithBrowserAutomationSignal_ReturnsUiAndEndToEndTests()
    {
        // Arrange
        var context = CreateContext(memberBody: "await Page.GotoAsync(\"/checkout\");");

        // Act
        var categories = TestMetadataClassifier.InferDeterministicCategories(context);

        // Assert
        Assert.Equal(["EndToEndTest", "UiTest"], categories);
        var decision = Assert.Single(TestMetadataClassifier.InferDeterministicCategoryDecisions(context));
        Assert.Equal("test-metadata.category.browser-automation", decision.RuleId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InferDeterministicMetadata_WithNoSpecificSignals_ReturnsSourceIntentAndConfidenceDecisions()
    {
        var metadata = TestMetadataClassifier.InferDeterministicMetadata(CreateContext(memberName: "Adds_numbers"));

        Assert.Equal(["UnitTest"], metadata.Categories);
        Assert.Equal("Deterministic", metadata.Source);
        Assert.Equal(0.60, metadata.Confidence);
        Assert.Contains(metadata.RuleDecisions, x => x.RuleId == "test-metadata.intent.fallback");
        Assert.Contains(metadata.RuleDecisions, x => x.RuleId == "test-metadata.confidence.deterministic");
        Assert.Contains(metadata.RuleDecisions, x => x.RuleId == "test-metadata.source.deterministic");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyLlmResult_WithAllowedCategories_ReturnsHybridDecision()
    {
        var deterministic = TestMetadataClassifier.InferDeterministicMetadata(CreateContext());
        var enriched = TestMetadataClassifier.ApplyLlmResult(
            deterministic,
            new LlmMetadataResult
            {
                Categories = ["SmokeTest", "Unsupported"],
                Intent = "Tests smoke behavior",
                Confidence = null
            },
            "test-metadata-v2");

        Assert.Equal(["SmokeTest", "UnitTest"], enriched.Categories);
        Assert.Equal("Hybrid", enriched.Source);
        Assert.Equal("test-metadata-v2", enriched.PromptVersion);
        Assert.Equal("Tests smoke behavior.", enriched.Intent);
        Assert.Contains(enriched.RuleDecisions, x => x.RuleId == "test-metadata.merge.allowed-llm-categories");
        Assert.Contains(enriched.RuleDecisions, x => x.RuleId == "test-metadata.intent.llm-normalized");
        Assert.Contains(enriched.RuleDecisions, x => x.RuleId == "test-metadata.source.hybrid");
        Assert.Contains(enriched.RuleDecisions, x => x.RuleId == "test-metadata.confidence.hybrid");
    }

    /// <summary>
    /// Verifies that category merging preserves deterministic categories and ignores unsupported model output.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MergeCategories_WithUnsupportedLlmCategories_FiltersUnsupportedValues()
    {
        // Arrange
        var deterministicCategories = new[] { "IntegrationTest" };
        var llmCategories = new[] { "SmokeTest", "Unsupported", "IntegrationTest" };

        // Act
        var categories = TestMetadataClassifier.MergeCategories(deterministicCategories, llmCategories);

        // Assert
        Assert.Equal(["IntegrationTest", "SmokeTest"], categories);
    }

    /// <summary>
    /// Verifies that fallback intents normalize method names into readable intent text.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CreateFallbackIntent_WithUnderscoreMethodName_ReturnsReadableSentence()
    {
        // Act
        var intent = TestMetadataClassifier.CreateFallbackIntent("Creates_order_when_cart_is_valid");

        // Assert
        Assert.Equal("Tests Creates order when cart is valid; ensures the expected behavior holds.", intent);
    }

    /// <summary>
    /// Verifies that confidence helpers use the expected deterministic and hybrid confidence curves.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ConfidenceHelpers_WithDifferentCategoryCounts_ReturnExpectedScores()
    {
        // Act
        var noCategoryScore = TestMetadataClassifier.GetDeterministicConfidence([]);
        var oneCategoryScore = TestMetadataClassifier.GetDeterministicConfidence(["UnitTest"]);
        var twoCategoryScore = TestMetadataClassifier.GetDeterministicConfidence(["ApiTest", "IntegrationTest"]);
        var manyCategoryScore = TestMetadataClassifier.GetDeterministicConfidence(
            ["ApiTest", "IntegrationTest", "SmokeTest"]);
        var cappedHybridScore = TestMetadataClassifier.GetHybridConfidence(
            ["ApiTest", "IntegrationTest", "SmokeTest", "RegressionTest", "EndToEndTest"]);

        // Assert
        Assert.Equal(0.40, noCategoryScore);
        Assert.Equal(0.60, oneCategoryScore);
        Assert.Equal(0.68, twoCategoryScore);
        Assert.Equal(0.72, manyCategoryScore);
        Assert.Equal(0.95, cappedHybridScore);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TestMetadataRuleDefinitions_AllRulesHaveUniqueVersionedIds()
    {
        var rules = TestMetadataRuleDefinitions.All;

        Assert.All(rules, rule =>
        {
            Assert.StartsWith("test-metadata.", rule.Id, StringComparison.Ordinal);
            Assert.Equal("1.0", rule.Version);
            Assert.Equal("TestMetadataEnrichment", rule.Category);
            Assert.False(string.IsNullOrWhiteSpace(rule.Description));
        });
        Assert.Equal(rules.Count, rules.Select(x => (x.Id, x.Version)).Distinct().Count());
    }

    private static TestMemberContext CreateContext(
        string memberName = "Test_method",
        List<string>? attributes = null,
        string memberBody = "",
        string objectBody = "",
        string testFilePath = "CalculatorTests.cs",
        string testProjectPath = "Calculator.Tests.csproj",
        string testFramework = "xUnit")
    {
        return new TestMemberContext
        {
            Member = new MemberEntity
            {
                Name = memberName,
                Attributes = attributes ?? [],
                FullString = memberBody
            },
            TestObject = new ObjectEntity
            {
                FullString = objectBody,
                TestFramework = testFramework
            },
            TestFile = new FileEntity
            {
                FilePath = testFilePath
            },
            TestProject = new CSharpProjectEntity
            {
                FilePath = testProjectPath
            }
        };
    }
}
