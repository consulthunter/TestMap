using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.MetadataEnrichment;
using TestMap.Persistence.Ef;
using TestMap.Rules.TestMetadata;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Services.StaticAnalysis.Enrichment;

public class TestMetadataEnrichmentService : ITestMetadataEnrichmentService
{
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

            var metadataDecision = TestMetadataClassifier.InferDeterministicMetadata(testMember);

            if (provider != null)
            {
                var llmResult = await TryEnrichWithModelAsync(
                    testMember,
                    metadataDecision.Categories,
                    provider,
                    enrichmentConfig,
                    cancellationToken);
                if (llmResult != null)
                    metadataDecision = TestMetadataClassifier.ApplyLlmResult(
                        metadataDecision,
                        llmResult,
                        enrichmentConfig.PromptVersion);
            }

            testMember.Member.TestCategories = metadataDecision.Categories;
            testMember.Member.TestIntent = metadataDecision.Intent;
            testMember.Member.TestMetadataSource = metadataDecision.Source;
            testMember.Member.TestMetadataConfidence = metadataDecision.Confidence;
            testMember.Member.TestMetadataPromptVersion = metadataDecision.PromptVersion;
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

    private static string CreatePrompt(
        TestMemberContext context,
        IReadOnlyCollection<string> deterministicCategories,
        MetadataEnrichmentConfig enrichmentConfig)
    {
        return $@"Classify this C# test method using only the allowed categories and write a short intent sentence.

Prompt version:
{enrichmentConfig.PromptVersion}

Allowed categories:
{string.Join(", ", TestMetadataClassifier.AllowedCategories)}

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
                .Where(x => TestMetadataClassifier.AllowedCategories.Contains(x, StringComparer.Ordinal))
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

}
