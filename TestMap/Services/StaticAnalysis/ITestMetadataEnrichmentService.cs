namespace TestMap.Services.StaticAnalysis;

public interface ITestMetadataEnrichmentService
{
    Task EnrichAsync(CancellationToken cancellationToken = default);
}
