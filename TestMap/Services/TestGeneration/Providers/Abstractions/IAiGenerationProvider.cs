using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Services.TestGeneration.Providers.Abstractions;

public interface IAiGenerationProvider
{
    AiProvider Provider { get; }

    Task CreateAsync(IAiProviderConfig providerConfig, AiProviderMode mode,
        CancellationToken cancellationToken = default);

    Task<string> GenerateAsync(string prompt, double temperature = 0.0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the exact TestMap-supplied message content segments sent for this prompt.
    /// This supports reproducible local estimates without changing the text-only response contract.
    /// </summary>
    IReadOnlyList<string> GetTokenizableInputSegments(string prompt);
}
