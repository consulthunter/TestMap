using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.MetadataEnrichment;
using TestMap.Persistence.Ef;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Services.StaticAnalysis.Enrichment;

public class TestMetadataEnrichmentService : ITestMetadataEnrichmentService
{
    private static readonly string[] AllowedCategories =
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

    private readonly ProjectContext _context;
    private readonly TestMapConfig _config;
    private readonly TestMapDbContext _dbContext;
    private readonly Dictionary<AiProvider, IAiGenerationProvider> _providers;

    public TestMetadataEnrichmentService(
        ProjectContext context,
        TestMapConfig config,
        TestMapDbContext dbContext,
        IEnumerable<IAiGenerationProvider> providers)
    {
        _context = context;
        _config = config;
        _dbContext = dbContext;
        _providers = providers.ToDictionary(x => x.Provider);
    }

    public async Task EnrichAsync(CancellationToken cancellationToken = default)
    {
        var testMembers = await (
                from member in _dbContext.Members
                join testObject in _dbContext.Objects on member.ObjectEntityId equals testObject.Id
                join testFile in _dbContext.Files on testObject.FileId equals testFile.Id
                join testProject in _dbContext.CSharpProjects on testFile.CSharpProjectId equals testProject.Id
                where member.IsTestMember && !member.IsGenerated
                select new TestMemberContext
                {
                    Member = member,
                    TestObject = testObject,
                    TestFile = testFile,
                    TestProject = testProject
                })
            .ToListAsync(cancellationToken);

        if (testMembers.Count == 0)
        {
            _context.Project.Logger?.Information("No persisted test members were found for metadata enrichment.");
            return;
        }

        var enrichmentConfig = _config.TestingConfig.MetadataEnrichmentConfig;
        if (!enrichmentConfig.Enabled)
        {
            _context.Project.Logger?.Information("Test metadata enrichment is disabled by configuration.");
            return;
        }

        var provider = enrichmentConfig.UseModel
            ? await TryGetProviderAsync(enrichmentConfig, cancellationToken)
            : null;
        var enrichedCount = 0;

        foreach (var testMember in testMembers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deterministicCategories = InferDeterministicCategories(testMember);
            var intent = CreateFallbackIntent(testMember.Member.Name);
            var categories = deterministicCategories;
            var source = "Deterministic";
            double? confidence = GetDeterministicConfidence(deterministicCategories);
            var promptVersion = string.Empty;

            if (provider != null)
            {
                var llmResult = await TryEnrichWithModelAsync(
                    testMember,
                    deterministicCategories,
                    provider,
                    enrichmentConfig,
                    cancellationToken);
                if (llmResult != null)
                {
                    categories = MergeCategories(deterministicCategories, llmResult.Categories);
                    if (!string.IsNullOrWhiteSpace(llmResult.Intent)) intent = NormalizeIntent(llmResult.Intent);

                    source = deterministicCategories.Count > 0 ? "Hybrid" : "Llm";
                    confidence = llmResult.Confidence ?? GetHybridConfidence(categories);
                    promptVersion = enrichmentConfig.PromptVersion;
                }
            }

            testMember.Member.TestCategories = categories;
            testMember.Member.TestIntent = intent;
            testMember.Member.TestMetadataSource = source;
            testMember.Member.TestMetadataConfidence = confidence;
            testMember.Member.TestMetadataPromptVersion = promptVersion;
            enrichedCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _context.Project.Logger?.Information(
            "Enriched metadata for {Count} persisted test members.",
            enrichedCount);
    }

    private async Task<IAiGenerationProvider?> TryGetProviderAsync(
        MetadataEnrichmentConfig enrichmentConfig,
        CancellationToken cancellationToken)
    {
        var providerType = enrichmentConfig.Provider;
        var providerConfig = _config.AiProviderConfig.GetProviderConfig(providerType);

        if (providerConfig == null ||
            string.IsNullOrWhiteSpace(providerConfig.Model) ||
            !_providers.ContainsKey(providerType))
        {
            _context.Project.Logger?.Warning(
                "Skipping LLM test metadata enrichment because provider {Provider} is not fully configured.",
                providerType);
            return null;
        }

        if (providerType != AiProvider.Ollama &&
            providerType != AiProvider.GoogleCloud &&
            providerType != AiProvider.CustomOpenAi &&
            string.IsNullOrWhiteSpace(providerConfig.ApiKey))
        {
            _context.Project.Logger?.Warning(
                "Skipping LLM test metadata enrichment because provider {Provider} has no API key configured.",
                providerType);
            return null;
        }

        var provider = _providers[providerType];
        try
        {
            await provider.CreateAsync(providerConfig, enrichmentConfig.Mode, cancellationToken);
            return provider;
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Warning(
                "Skipping LLM test metadata enrichment because provider {Provider} could not be initialized: {Message}",
                providerType,
                ex.Message);
            return null;
        }
    }

    private async Task<LlmMetadataResult?> TryEnrichWithModelAsync(
        TestMemberContext context,
        IReadOnlyCollection<string> deterministicCategories,
        IAiGenerationProvider provider,
        MetadataEnrichmentConfig enrichmentConfig,
        CancellationToken cancellationToken)
    {
        try
        {
            var prompt = CreatePrompt(context, deterministicCategories, enrichmentConfig);
            var response = await provider.GenerateAsync(prompt, enrichmentConfig.Temperature, cancellationToken);
            return ParseLlmResult(response);
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Warning(
                "LLM test metadata enrichment failed for {MemberName}: {Message}",
                context.Member.Name,
                ex.Message);
            return null;
        }
    }

    private static List<string> InferDeterministicCategories(TestMemberContext context)
    {
        var categories = new HashSet<string>(StringComparer.Ordinal);
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
            categories.Add("Benchmark");

        if (attributes.Any(x => x.Contains("Theory", StringComparison.OrdinalIgnoreCase) ||
                                x.Contains("TestCase", StringComparison.OrdinalIgnoreCase) ||
                                x.Contains("DataRow", StringComparison.OrdinalIgnoreCase) ||
                                x.Contains("DataTestMethod", StringComparison.OrdinalIgnoreCase)))
            categories.Add("ParameterizedTest");

        if (combinedText.Contains("WebApplicationFactory", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("TestServer", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("HttpClient", StringComparison.OrdinalIgnoreCase) ||
            context.TestProject.FilePath.Contains(".Api", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add("ApiTest");
            categories.Add("IntegrationTest");
        }

        if (combinedText.Contains("Playwright", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Selenium", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Page.GotoAsync", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add("UiTest");
            categories.Add("EndToEndTest");
        }

        if (combinedText.Contains("FsCheck", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Property", StringComparison.OrdinalIgnoreCase))
            categories.Add("PropertyTest");

        if (context.Member.Name.Contains("Smoke", StringComparison.OrdinalIgnoreCase)) categories.Add("SmokeTest");

        if (context.Member.Name.Contains("Regression", StringComparison.OrdinalIgnoreCase))
            categories.Add("RegressionTest");

        if (combinedText.Contains("DbContext", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("SqlConnection", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("IHost", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Container", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Docker", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("File.", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Directory.", StringComparison.OrdinalIgnoreCase))
            categories.Add("IntegrationTest");

        if (categories.Count == 0) categories.Add("UnitTest");

        return categories.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static List<string> MergeCategories(
        IReadOnlyCollection<string> deterministicCategories,
        IReadOnlyCollection<string> llmCategories)
    {
        var merged = new HashSet<string>(deterministicCategories, StringComparer.Ordinal);
        foreach (var category in llmCategories.Where(AllowedCategories.Contains)) merged.Add(category);

        if (merged.Count == 0) merged.Add("UnitTest");

        return merged.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static double GetDeterministicConfidence(IReadOnlyCollection<string> categories)
    {
        return categories.Count switch
        {
            0 => 0.40,
            1 => 0.60,
            2 => 0.68,
            _ => 0.72
        };
    }

    private static double GetHybridConfidence(IReadOnlyCollection<string> categories)
    {
        return Math.Min(0.95, 0.72 + categories.Count * 0.05);
    }

    private static string CreateFallbackIntent(string memberName)
    {
        var readableName = string.IsNullOrWhiteSpace(memberName)
            ? "the target behavior"
            : memberName.Replace('_', ' ').Trim();

        return $"Tests {readableName}; ensures the expected behavior holds.";
    }

    private static string NormalizeIntent(string intent)
    {
        var trimmed = intent.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return string.Empty;

        if (!trimmed.EndsWith('.')) trimmed += ".";

        return trimmed;
    }

    private static string CreatePrompt(
        TestMemberContext context,
        IReadOnlyCollection<string> deterministicCategories,
        MetadataEnrichmentConfig enrichmentConfig)
    {
        return $@"Classify this C# test method using only the allowed categories and write a short intent sentence.

Prompt version:
{enrichmentConfig.PromptVersion}

Allowed categories:
{string.Join(", ", AllowedCategories)}

Return strict JSON in this shape:
{{""categories"":[""UnitTest""],""intent"":""Tests condition; ensures expected behavior."",""confidence"":0.82}}

Rules:
- Categories must come only from the allowed list.
- Prefer 1-{enrichmentConfig.MaxCategories} categories.
- Intent must be one sentence.
- Intent should follow the style: Tests <condition>; ensures <behavior>.
- Confidence must be between 0.0 and 1.0.
- Do not include markdown or explanation.

Test framework:
{context.TestObject.TestFramework}

Existing deterministic categories:
{string.Join(", ", deterministicCategories)}

Test class file:
{context.TestFile.FilePath}

Test project:
{context.TestProject.FilePath}

Containing test class:
{context.TestObject.FullString}

Target test member:
{context.Member.FullString}";
    }

    private static LlmMetadataResult? ParseLlmResult(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;

        var json = ExtractJson(response);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var result = JsonSerializer.Deserialize<LlmMetadataResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null) return null;

            result.Categories = result.Categories
                .Where(x => AllowedCategories.Contains(x, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .ToList();

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractJson(string response)
    {
        var trimmed = response.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var parts = trimmed.Split("```", StringSplitOptions.RemoveEmptyEntries);
            var candidate = parts.FirstOrDefault(x => x.Contains('{'));
            if (!string.IsNullOrWhiteSpace(candidate))
                trimmed = candidate.Replace("json", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start
            ? trimmed[start..(end + 1)]
            : string.Empty;
    }

    private sealed class TestMemberContext
    {
        public required Persistence.Ef.Entities.Code.MemberEntity Member { get; init; }
        public required Persistence.Ef.Entities.Code.ObjectEntity TestObject { get; init; }
        public required Persistence.Ef.Entities.Code.FileEntity TestFile { get; init; }
        public required Persistence.Ef.Entities.Code.CSharpProjectEntity TestProject { get; init; }
    }

    private sealed class LlmMetadataResult
    {
        public List<string> Categories { get; set; } = new();
        public string Intent { get; set; } = string.Empty;
        public double? Confidence { get; set; }
    }
}