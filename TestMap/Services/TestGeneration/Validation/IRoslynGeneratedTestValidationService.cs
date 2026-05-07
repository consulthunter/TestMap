using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Validation;

public interface IRoslynGeneratedTestValidationService
{
    Task<RoslynGeneratedTestDiagnosticSnapshot> CaptureBeforeAsync(
        CandidateMethodContext context,
        CancellationToken cancellationToken = default);

    Task<RoslynGeneratedTestValidationResult> ValidateAfterApplicationAsync(
        CandidateMethodContext context,
        RoslynGeneratedTestDiagnosticSnapshot before,
        CancellationToken cancellationToken = default);
}

