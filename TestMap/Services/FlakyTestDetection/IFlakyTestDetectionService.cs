using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public interface IFlakyTestDetectionService
{
    Task<FlakyTestDetectionResult> DetectAsync(
        FlakyTestDetectionRequest request,
        CancellationToken cancellationToken = default);
}