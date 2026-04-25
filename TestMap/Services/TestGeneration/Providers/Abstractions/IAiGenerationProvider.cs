using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Services.TestGeneration.Providers.Abstractions;

public interface IAiGenerationProvider
{
    AiProvider Provider { get; }

    Task CreateAsync(IAiProviderConfig providerConfig, AiProviderMode mode,
        CancellationToken cancellationToken = default);

    Task<string> GenerateAsync(string prompt, double temperature = 0.0, CancellationToken cancellationToken = default);
}