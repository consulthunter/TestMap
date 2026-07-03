using TestMap.Models.Rules;

namespace TestMap.Rules.Generation;

/// <summary>
/// Rule definitions for <see cref="TestMap.Services.TestGeneration.Editing.BasicExtensionPatchApplicationService"/>.
/// Each rule corresponds to a discrete decision recorded during patch application.
/// </summary>
public static class BasicExtensionPatchRuleDefinitions
{
    private const string Version = "1.0";
    private const string Category = "BasicExtensionPatch";

    public static RuleDefinition PatchFileNotFound { get; } = Define(
        "basic-extension.patch.file-not-found",
        "Patch file not found",
        "The test file specified by the candidate context was not found on disk.");

    public static RuleDefinition PatchTargetClassMissing { get; } = Define(
        "basic-extension.patch.target-class-missing",
        "Patch target class missing",
        "The target test class name was not found in the parsed test file.");

    public static RuleDefinition PatchUsingSkipped { get; } = Define(
        "basic-extension.patch.using-skipped",
        "Patch using skipped",
        "A using directive in the patch was already present in the file and was skipped.");

    public static RuleDefinition PatchMalformedHelper { get; } = Define(
        "basic-extension.patch.malformed-helper",
        "Patch malformed helper",
        "A helper method in the patch did not parse as a valid C# method declaration.");

    public static RuleDefinition PatchDuplicateHelper { get; } = Define(
        "basic-extension.patch.duplicate-helper",
        "Patch duplicate helper",
        "A helper method name in the patch already exists in the target class.");

    public static RuleDefinition PatchMalformedTestMethod { get; } = Define(
        "basic-extension.patch.malformed-test-method",
        "Patch malformed test method",
        "The test method in the patch did not parse as a valid C# method declaration.");

    public static RuleDefinition PatchDuplicateTestMethod { get; } = Define(
        "basic-extension.patch.duplicate-test-method",
        "Patch duplicate test method",
        "The test method name in the patch already exists in the target class.");

    public static RuleDefinition PatchApplied { get; } = Define(
        "basic-extension.patch.applied",
        "Patch applied",
        "The patch was applied successfully: usings, helpers, and the test method were inserted.");

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        PatchFileNotFound,
        PatchTargetClassMissing,
        PatchUsingSkipped,
        PatchMalformedHelper,
        PatchDuplicateHelper,
        PatchMalformedTestMethod,
        PatchDuplicateTestMethod,
        PatchApplied
    ];

    private static RuleDefinition Define(string id, string name, string description)
    {
        return new RuleDefinition
        {
            Id = id,
            Version = Version,
            Name = name,
            Description = description,
            Category = Category
        };
    }
}
