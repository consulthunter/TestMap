using TestMap.Models.Rules;

namespace TestMap.Rules.Generation;

public static class GenerationAppendRuleDefinitions
{
    private const string Version = "1.0";
    private const string Category = "GenerationAppend";

    public static RuleDefinition AppendTargetSelected { get; } = Define(
        "generation.append.target-selected",
        "Append target selected",
        "A syntax-tree object was selected as the append target for the generated test method.");

    public static RuleDefinition AppendTargetMissing { get; } = Define(
        "generation.append.target-missing",
        "Append target missing",
        "The intended syntax-tree object for appending the generated test method was not found.");

    public static RuleDefinition GeneratedMethodParsed { get; } = Define(
        "generation.append.generated-method-parsed",
        "Generated method parsed",
        "The generated test method parsed as a C# method member.");

    public static RuleDefinition GeneratedMethodParseFailed { get; } = Define(
        "generation.append.generated-method-parse-failed",
        "Generated method parse failed",
        "The generated test method did not parse as a C# method member.");

    public static RuleDefinition GeneratedMethodInserted { get; } = Define(
        "generation.append.generated-method-inserted",
        "Generated method inserted",
        "The generated test method was inserted into the selected syntax-tree object.");

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        AppendTargetSelected,
        AppendTargetMissing,
        GeneratedMethodParsed,
        GeneratedMethodParseFailed,
        GeneratedMethodInserted
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
