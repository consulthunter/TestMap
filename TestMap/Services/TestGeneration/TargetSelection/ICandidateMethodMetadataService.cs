using Microsoft.CodeAnalysis;
using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.TargetSelection;

public interface ICandidateMethodMetadataService
{
    CandidateMethodMetadata Build(string methodSource, string containingClassSource);

    Task<CandidateMethodMetadata> BuildAsync(
        Document? document,
        int sourceStartPosition,
        int sourceEndPosition,
        string methodSource,
        string containingClassSource,
        CancellationToken cancellationToken = default);
}
