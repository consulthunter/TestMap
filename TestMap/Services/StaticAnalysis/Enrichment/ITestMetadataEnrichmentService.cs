namespace TestMap.Services.StaticAnalysis.Enrichment;

public interface ITestMetadataEnrichmentService
{
    Task EnrichAsync(CancellationToken cancellationToken = default);
}