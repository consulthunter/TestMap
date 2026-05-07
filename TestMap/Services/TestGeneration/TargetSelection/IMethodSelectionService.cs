using TestMap.Models.Configuration;
using TestMap.Models.Experiment;

namespace TestMap.Services.TestGeneration.TargetSelection;

/// <summary>
/// Service for selecting candidate methods for test generation experiments.
/// </summary>
public interface IMethodSelectionService
{
    /// <summary>
    /// Selects candidate methods based on coverage criteria specified in the experiment configuration.
    /// </summary>
    /// <param name="config">Experiment configuration with selection criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of candidate methods ready for test generation</returns>
    Task<List<CandidateMethod>> SelectCandidateMethodsAsync(
        ExperimentConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a candidate method including its context
    /// (containing class, test framework, example tests, etc.)
    /// </summary>
    /// <param name="memberId">Database ID of the member</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Candidate method with full context for generation</returns>
    Task<CandidateMethodContext?> GetMethodContextAsync(
        int memberId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Extended context for a candidate method, including all information needed for test generation.
/// </summary>
public class CandidateMethodContext
{
    public required CandidateMethod Method { get; init; }
    public required string MethodSignature { get; init; }
    public required string ContainingClass { get; init; }
    public required string TestNamespace { get; init; }
    public required string TestClassName { get; init; }
    public required string TestFilePath { get; init; }
    public required string SourceFilePath { get; init; }
    public required CandidateSourceLocation SourceLocation { get; init; }
    public CandidateTestLocation? TestLocation { get; init; }
    public required string SourceProjectPath { get; init; }
    public required string TestProjectPath { get; init; }
    public required string TargetBuildFramework { get; init; }
    public required string SolutionFilePath { get; init; }
    public required string ExampleTest { get; init; }
    public required string ExampleTestMetadataSummary { get; init; }
    public required string ProjectTestMetadataSummary { get; init; }
    public required string TestClass { get; init; }
    public required string TestFileContents { get; init; }
    public required string TestSupportContext { get; init; }
    public required string TestFramework { get; init; }
    public required string TestDependencies { get; init; }
    public required string CoverageGapSummary { get; init; }
}

public sealed class CandidateSourceLocation
{
    public string SourceFilePath { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public int StartPosition { get; init; }
    public int EndPosition { get; init; }
}

public sealed class CandidateTestLocation
{
    public string? TestFilePath { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
    public string? TestProjectPath { get; init; }
}
