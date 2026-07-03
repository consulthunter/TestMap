using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models.Generation;
using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Rules.Generation;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Editing;

/// <summary>
/// Applies a <see cref="BasicExtensionPatch"/> to the test file identified by a
/// <see cref="CandidateMethodContext"/> using syntax-level Roslyn editing.
/// </summary>
/// <remarks>
/// Ordered steps:
/// <list type="number">
///   <item>Verify the test file exists.</item>
///   <item>Read the original file contents (captured for rollback).</item>
///   <item>Parse the file as a <see cref="CompilationUnitSyntax"/>.</item>
///   <item>Locate the target class by <see cref="CandidateMethodContext.TestClassName"/>.</item>
///   <item>Build a set of existing using directive names for deduplication.</item>
///   <item>Process <see cref="BasicExtensionPatch.UsingsToAdd"/>: skip duplicates, collect new directives.</item>
///   <item>Process <see cref="BasicExtensionPatch.HelperMethodsToAdd"/>: parse and validate each helper.</item>
///   <item>Parse <see cref="BasicExtensionPatch.TestMethod"/>.</item>
///   <item>Check the test method name for duplicates in the target class.</item>
///   <item>Apply all edits and write the file.</item>
/// </list>
/// Roslyn is used for syntax-level editing only, not as a compile authority.
/// </remarks>
public sealed class BasicExtensionPatchApplicationService : IBasicExtensionPatchApplicationService
{
    /// <inheritdoc/>
    public BasicExtensionPatchApplicationResult Apply(
        CandidateMethodContext context,
        BasicExtensionPatch patch)
    {
        var decisions = new List<RuleDecisionRecord>();

        // Step 1 — verify file exists
        if (!File.Exists(context.TestFilePath))
        {
            decisions.Add(MakeDecision(
                BasicExtensionPatchRuleDefinitions.PatchFileNotFound,
                "TestFileMissing",
                Evidence("PatchApplier", "FilePath", context.TestFilePath)));
            return Failure(
                "TestFileMissing",
                $"Test file '{context.TestFilePath}' was not found.",
                originalContents: null,
                decisions);
        }

        // Step 2 — read original contents for rollback
        var originalContents = File.ReadAllText(context.TestFilePath);

        // Step 3 — parse compilation unit
        var root = CSharpSyntaxTree.ParseText(originalContents).GetCompilationUnitRoot();

        // Step 4 — find target class
        var classNode = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == context.TestClassName);

        if (classNode == null)
        {
            decisions.Add(MakeDecision(
                BasicExtensionPatchRuleDefinitions.PatchTargetClassMissing,
                "TargetClassMissing",
                Evidence("PatchApplier", "ExpectedClassName", context.TestClassName),
                Evidence("PatchApplier", "FilePath", context.TestFilePath)));
            return Failure(
                "TargetClassMissing",
                $"Target test class '{context.TestClassName}' was not found in '{context.TestFilePath}'.",
                originalContents,
                decisions);
        }

        // Step 5 — collect existing using names for deduplication
        var existingUsings = new HashSet<string>(
            root.Usings.Select(u => u.Name?.ToString() ?? string.Empty),
            StringComparer.Ordinal);

        // Step 6 — process UsingsToAdd
        var newUsingDirectives = new List<UsingDirectiveSyntax>();
        foreach (var entry in patch.UsingsToAdd)
        {
            var normalized = NormalizeUsing(entry);
            if (string.IsNullOrEmpty(normalized) || existingUsings.Contains(normalized))
            {
                decisions.Add(MakeDecision(
                    BasicExtensionPatchRuleDefinitions.PatchUsingSkipped,
                    "UsingSkipped",
                    Evidence("PatchApplier", "Using", entry)));
                continue;
            }

            newUsingDirectives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(normalized)));
            existingUsings.Add(normalized);
        }

        // Step 7 — process HelperMethodsToAdd
        // Seed the name set from existing class members so helpers can't collide with them.
        var existingMemberNames = new HashSet<string>(
            classNode.Members
                .OfType<MethodDeclarationSyntax>()
                .Select(m => m.Identifier.Text),
            StringComparer.Ordinal);

        var helperMethods = new List<MethodDeclarationSyntax>();
        foreach (var helperCode in patch.HelperMethodsToAdd)
        {
            if (SyntaxFactory.ParseMemberDeclaration(helperCode) is not MethodDeclarationSyntax helper)
            {
                decisions.Add(MakeDecision(
                    BasicExtensionPatchRuleDefinitions.PatchMalformedHelper,
                    "MalformedHelper",
                    Evidence("PatchApplier", "HelperCode",
                        helperCode[..Math.Min(120, helperCode.Length)])));
                return Failure(
                    "MalformedHelper",
                    "A helper method in the patch did not parse as a valid C# method declaration.",
                    originalContents,
                    decisions);
            }

            var helperName = helper.Identifier.Text;
            if (existingMemberNames.Contains(helperName))
            {
                decisions.Add(MakeDecision(
                    BasicExtensionPatchRuleDefinitions.PatchDuplicateHelper,
                    "DuplicateHelper",
                    Evidence("PatchApplier", "HelperName", helperName),
                    Evidence("PatchApplier", "ClassName", context.TestClassName)));
                return Failure(
                    "DuplicateHelper",
                    $"Helper method '{helperName}' already exists in class '{context.TestClassName}'.",
                    originalContents,
                    decisions);
            }

            helperMethods.Add(helper);
            // Track so a subsequent helper with the same name is also rejected.
            existingMemberNames.Add(helperName);
        }

        // Step 8 — parse test method
        if (SyntaxFactory.ParseMemberDeclaration(patch.TestMethod) is not MethodDeclarationSyntax testMethod)
        {
            decisions.Add(MakeDecision(
                BasicExtensionPatchRuleDefinitions.PatchMalformedTestMethod,
                "MalformedTestMethod",
                Evidence("PatchApplier", "TestMethodCode",
                    patch.TestMethod[..Math.Min(120, patch.TestMethod.Length)])));
            return Failure(
                "MalformedTestMethod",
                "The test method in the patch did not parse as a valid C# method declaration.",
                originalContents,
                decisions);
        }

        // Step 9 — check for duplicate test method name
        var testMethodName = testMethod.Identifier.Text;
        if (existingMemberNames.Contains(testMethodName))
        {
            decisions.Add(MakeDecision(
                BasicExtensionPatchRuleDefinitions.PatchDuplicateTestMethod,
                "DuplicateTestMethod",
                Evidence("PatchApplier", "TestMethodName", testMethodName),
                Evidence("PatchApplier", "ClassName", context.TestClassName)));
            return Failure(
                "DuplicateTestMethod",
                $"Test method '{testMethodName}' already exists in class '{context.TestClassName}'.",
                originalContents,
                decisions);
        }

        // Step 10 — apply all edits and write
        var updatedRoot = newUsingDirectives.Count > 0
            ? root.AddUsings(newUsingDirectives.ToArray())
            : root;

        // Re-find class in the updated tree (using additions shift node identity).
        var updatedClass = updatedRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == context.TestClassName);

        var membersToAdd = helperMethods
            .Cast<MemberDeclarationSyntax>()
            .Append(testMethod)
            .ToArray();

        var classWithNewMembers = updatedClass.AddMembers(membersToAdd);
        updatedRoot = updatedRoot.ReplaceNode(updatedClass, classWithNewMembers);

        File.WriteAllText(context.TestFilePath, updatedRoot.NormalizeWhitespace().ToFullString());

        decisions.Add(MakeDecision(
            BasicExtensionPatchRuleDefinitions.PatchApplied,
            "Success",
            Evidence("PatchApplier", "TestMethodName", testMethodName),
            Evidence("PatchApplier", "AppliedUsingCount", newUsingDirectives.Count.ToString()),
            Evidence("PatchApplier", "AppliedHelperCount", helperMethods.Count.ToString()),
            Evidence("PatchApplier", "ClassName", context.TestClassName)));

        return new BasicExtensionPatchApplicationResult
        {
            Success = true,
            PatchApplicationOutcome = "Success",
            AppliedUsingCount = newUsingDirectives.Count,
            AppliedHelperCount = helperMethods.Count,
            AppliedTestMethodName = testMethodName,
            OriginalFileContents = originalContents,
            RuleDecisions = decisions
        };
    }

    /// <summary>
    /// Strips the optional "using " prefix and trailing ";" from a using directive string
    /// so it can be compared against Roslyn's <c>UsingDirectiveSyntax.Name.ToString()</c>.
    /// </summary>
    private static string NormalizeUsing(string value)
    {
        var s = value.Trim();
        if (s.StartsWith("using ", StringComparison.Ordinal))
            s = s["using ".Length..].Trim();
        if (s.EndsWith(";", StringComparison.Ordinal))
            s = s[..^1].Trim();
        return s;
    }

    private static BasicExtensionPatchApplicationResult Failure(
        string outcome,
        string errorMessage,
        string? originalContents,
        IReadOnlyList<RuleDecisionRecord> decisions)
    {
        return new BasicExtensionPatchApplicationResult
        {
            Success = false,
            PatchApplicationOutcome = outcome,
            ErrorMessage = errorMessage,
            OriginalFileContents = originalContents,
            RuleDecisions = decisions
        };
    }

    private static RuleDecisionRecord MakeDecision(
        RuleDefinition rule,
        string value,
        params RuleEvidenceRecord[] evidence)
    {
        return RuleDecisionFactory.CreateDecision(
            "PatchApplication",
            value,
            rule,
            RuleConfidence.High,
            evidence,
            rule.Description);
    }

    private static RuleEvidenceRecord Evidence(string source, string key, string value)
    {
        return RuleDecisionFactory.CreateEvidence(source, key, value);
    }
}
