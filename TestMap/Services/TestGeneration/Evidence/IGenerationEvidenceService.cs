namespace TestMap.Services.TestGeneration.Evidence;

public interface IGenerationEvidenceService
{
    Task<GenerationEvidencePackage> BuildAsync(
        GenerationEvidenceOptions options,
        CancellationToken cancellationToken = default);
}
