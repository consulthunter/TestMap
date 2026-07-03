namespace TestMap.Models.Generation;

/// <summary>
/// Structured patch produced by a Basic Extension generation step.
/// Carries the minimal file-level additions needed for the new test:
/// usings, optional helper methods, and the test method itself.
/// </summary>
public sealed class BasicExtensionPatch
{
    /// <summary>
    /// Fully-qualified namespace strings to add, e.g. "System.IO".
    /// Must not include the "using" keyword or trailing semicolon.
    /// The patch applier skips entries already present in the file.
    /// </summary>
    public IReadOnlyList<string> UsingsToAdd { get; init; } = [];

    /// <summary>
    /// Full C# method declarations for private helpers required by the new test.
    /// Each entry must parse as a valid <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax"/>.
    /// </summary>
    public IReadOnlyList<string> HelperMethodsToAdd { get; init; } = [];

    /// <summary>
    /// Full C# method declaration for the new test method.
    /// Must parse as a valid <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax"/>.
    /// </summary>
    public required string TestMethod { get; init; }

    /// <summary>
    /// Optional hint for the test method name, used when the method name cannot be
    /// derived from the parsed declaration (e.g. for repair prompts that need a name
    /// before the method is applied).
    /// </summary>
    public string? TestMethodName { get; init; }

    /// <summary>
    /// Free-text explanation of why these additions are needed.
    /// Used for logging and experiment analysis; not applied to the file.
    /// </summary>
    public string IntegrationRationale { get; init; } = string.Empty;
}
