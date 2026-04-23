using TestMap.Models.Configuration.Testing.FlakyDetection;

namespace TestMap.Models.FlakyTestDetection;

public sealed record FlakyTestDetectionRequest(
    string RunId,
    IReadOnlyList<TestExecutionResultModel> CurrentResults,
    FlakyTestDetectionConfig Config);
