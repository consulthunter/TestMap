using System.Text.Json;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Generation;
using TestMap.Services.TestGeneration.Editing;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Execution;

/// <summary>
/// Applies a generated test to an existing test file using the Basic Extension strategy.
/// </summary>
/// <remarks>
/// Execution order:
/// <list type="number">
///   <item>Verify the test file exists (precondition).</item>
///   <item>Verify the test class name is set (precondition).</item>
///   <item>Try to parse the generated response as a <see cref="BasicExtensionPatch"/> JSON object.</item>
///   <item>If parsing succeeds: apply the patch via <see cref="IBasicExtensionPatchApplicationService"/>.</item>
///   <item>If parsing fails: fall back to the legacy method-only append path and log a warning.</item>
/// </list>
/// The fallback preserves backward compatibility for providers or configurations that have
/// not yet adopted the structured patch output format.
/// </remarks>
public sealed class BasicExtensionTestActionExecutor : ITestActionExecutor
{
    private readonly ITestCodeEditService _editor;
    private readonly IBasicExtensionPatchApplicationService _patchApplier;

    public BasicExtensionTestActionExecutor(
        ITestCodeEditService editor,
        IBasicExtensionPatchApplicationService patchApplier)
    {
        _editor = editor;
        _patchApplier = patchApplier;
    }

    public TestActionExecutorMode Mode => TestActionExecutorMode.BasicExtension;

    public Task<TestActionExecutionResult> ExecuteAsync(
        CandidateMethodContext context,
        string generatedTest,
        string? generatedTestMethodName,
        CancellationToken cancellationToken = default)
    {
        // Precondition 1: test file must exist on disk.
        // An empty path or a missing file means this is not a valid extension target.
        if (string.IsNullOrEmpty(context.TestFilePath) || !File.Exists(context.TestFilePath))
            return Task.FromResult(new TestActionExecutionResult
            {
                Success = false,
                ActionKind = CandidateActionKind.ExtendExistingTestSuite,
                PatchApplicationOutcome = "TestFileMissing",
                ErrorMessage =
                    $"Test file '{context.TestFilePath}' was not found. " +
                    "Basic Extension requires an existing test file."
            });

        // Precondition 2: test class name must be set.
        if (string.IsNullOrEmpty(context.TestClassName))
            return Task.FromResult(new TestActionExecutionResult
            {
                Success = false,
                ActionKind = CandidateActionKind.ExtendExistingTestSuite,
                PatchApplicationOutcome = "TestClassNameMissing",
                ErrorMessage = "TestClassName is required for Basic Extension mode."
            });

        // Try the structured patch path first.
        var patch = TryParsePatch(generatedTest);
        if (patch != null)
        {
            var patchResult = _patchApplier.Apply(context, patch);
            return Task.FromResult(new TestActionExecutionResult
            {
                Success = patchResult.Success,
                AppliedFilePath = patchResult.Success ? context.TestFilePath : null,
                AppliedTestMethodName = patchResult.AppliedTestMethodName,
                ActionKind = CandidateActionKind.ExtendExistingTestSuite,
                PatchApplicationOutcome = patchResult.PatchApplicationOutcome,
                AppliedUsingCount = patchResult.AppliedUsingCount,
                AppliedHelperCount = patchResult.AppliedHelperCount,
                ErrorMessage = patchResult.Success ? null : patchResult.ErrorMessage,
                RuleDecisions = patchResult.RuleDecisions
            });
        }

        // Fallback: method-only append (legacy behavior for non-JSON model responses).
        // This preserves compatibility when the model does not produce a structured patch.
        var appendResult = _editor.AppendTestMethodWithResult(context, generatedTest);
        return Task.FromResult(new TestActionExecutionResult
        {
            Success = appendResult.Success,
            AppliedFilePath = context.TestFilePath,
            AppliedTestMethodName = generatedTestMethodName,
            ActionKind = CandidateActionKind.ExtendExistingTestSuite,
            ErrorMessage = appendResult.Success
                ? null
                : appendResult.ErrorMessage ?? $"Failed to append generated test to {context.TestFilePath}.",
            RuleDecisions = appendResult.RuleDecisions
        });
    }

    /// <summary>
    /// Tries to parse <paramref name="response"/> as a <see cref="BasicExtensionPatch"/> JSON object.
    /// Strips markdown code fences before parsing.
    /// Returns null if the response is not valid JSON, does not match the schema,
    /// or does not contain a non-empty <see cref="BasicExtensionPatch.TestMethod"/>.
    /// </summary>
    private static BasicExtensionPatch? TryParsePatch(string response)
    {
        var json = ExtractJsonBlock(response);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var patch = JsonSerializer.Deserialize<BasicExtensionPatch>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return string.IsNullOrWhiteSpace(patch?.TestMethod) ? null : patch;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the JSON object from a response that may be wrapped in markdown code fences.
    /// </summary>
    private static string ExtractJsonBlock(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return string.Empty;

        var trimmed = response.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var parts = trimmed.Split("```", StringSplitOptions.RemoveEmptyEntries);
            var candidate = parts.FirstOrDefault(x => x.Contains('{'));
            if (candidate != null)
                return candidate
                    .Replace("json", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : string.Empty;
    }
}
