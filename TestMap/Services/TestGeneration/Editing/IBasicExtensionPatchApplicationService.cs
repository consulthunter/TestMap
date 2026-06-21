using TestMap.Models.Generation;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Editing;

/// <summary>
/// Applies a <see cref="BasicExtensionPatch"/> to the test file identified by a
/// <see cref="CandidateMethodContext"/>.
/// Uses syntax-level Roslyn editing to add usings, helpers, and the test method
/// deterministically.  Rejects malformed or duplicate additions before any write.
/// </summary>
public interface IBasicExtensionPatchApplicationService
{
    /// <summary>
    /// Applies <paramref name="patch"/> to the test file specified in <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Candidate context carrying the test file path and target class name.</param>
    /// <param name="patch">Patch produced by the generation step.</param>
    /// <returns>
    /// A result describing success or the specific failure outcome.
    /// On failure the file is not written; on success the file is updated in place.
    /// </returns>
    BasicExtensionPatchApplicationResult Apply(
        CandidateMethodContext context,
        BasicExtensionPatch patch);
}
