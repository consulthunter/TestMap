using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Validation;

public sealed class RoslynGeneratedTestValidationService : IRoslynGeneratedTestValidationService
{
    private readonly IStaticAnalysisWorkspace _workspace;

    public RoslynGeneratedTestValidationService(IStaticAnalysisWorkspace workspace)
    {
        _workspace = workspace;
    }

    public async Task<RoslynGeneratedTestDiagnosticSnapshot> CaptureBeforeAsync(
        CandidateMethodContext context,
        CancellationToken cancellationToken = default)
    {
        var loadResult = await TryOpenTestDocumentAsync(context, cancellationToken);
        if (loadResult.SkipReason != null)
            return RoslynGeneratedTestDiagnosticSnapshot.Skip(loadResult.SkipReason);

        var document = loadResult.Document;
        if (document == null)
            return RoslynGeneratedTestDiagnosticSnapshot.Skip("Test document could not be loaded with Roslyn.");

        return await CaptureDocumentDiagnosticsAsync(document, cancellationToken);
    }

    public async Task<RoslynGeneratedTestValidationResult> ValidateAfterApplicationAsync(
        CandidateMethodContext context,
        RoslynGeneratedTestDiagnosticSnapshot before,
        CancellationToken cancellationToken = default)
    {
        if (!before.Captured)
            return RoslynGeneratedTestValidationResult.Skip(before.SkipReason ?? "Baseline diagnostics were not captured.");

        var loadResult = await TryOpenTestDocumentAsync(context, cancellationToken);
        if (loadResult.SkipReason != null)
            return RoslynGeneratedTestValidationResult.Skip(loadResult.SkipReason);

        var document = loadResult.Document;
        if (document == null)
            return RoslynGeneratedTestValidationResult.Skip("Test document could not be loaded with Roslyn.");

        if (!File.Exists(context.TestFilePath))
            return RoslynGeneratedTestValidationResult.Skip("Test file does not exist after generated test application.");

        var changedText = await File.ReadAllTextAsync(context.TestFilePath, cancellationToken);
        var changedDocument = document.WithText(SourceText.From(changedText));
        var after = await CaptureDocumentDiagnosticsAsync(changedDocument, cancellationToken);
        var newDiagnostics = FilterInheritedTestFrameworkResolutionDiagnostics(
            before.Diagnostics,
            DiffDiagnostics(before.Diagnostics, after.Diagnostics));
        var newErrors = newDiagnostics
            .Where(x => string.Equals(x.Severity, DiagnosticSeverity.Error.ToString(), StringComparison.Ordinal))
            .ToList();

        return new RoslynGeneratedTestValidationResult
        {
            Succeeded = newErrors.Count == 0,
            Skipped = false,
            FailureSummary = newErrors.Count == 0
                ? null
                : $"Generated test introduced {newErrors.Count} Roslyn error diagnostic(s).",
            Before = before,
            After = after,
            NewDiagnostics = newDiagnostics
        };
    }

    private async Task<TestDocumentLoadResult> TryOpenTestDocumentAsync(
        CandidateMethodContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.TestFilePath) ||
            string.IsNullOrWhiteSpace(context.TestProjectPath))
            return new TestDocumentLoadResult(null, null);

        _workspace.ClearWorkspaceFailures();
        Project? project = null;
        if (!string.IsNullOrWhiteSpace(context.SolutionFilePath) && File.Exists(context.SolutionFilePath))
        {
            var solution = await _workspace.OpenSolutionAsync(context.SolutionFilePath, cancellationToken);
            project = solution.Projects.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.FilePath) &&
                string.Equals(
                    Path.GetFullPath(x.FilePath),
                    Path.GetFullPath(context.TestProjectPath),
                    StringComparison.OrdinalIgnoreCase));
        }

        if (project == null && File.Exists(context.TestProjectPath))
            project = await _workspace.OpenProjectAsync(context.TestProjectPath, cancellationToken);

        var workspaceFailures = _workspace.WorkspaceFailures
            .Where(x => x.StartsWith("Failure:", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (workspaceFailures.Count > 0)
        {
            return new TestDocumentLoadResult(
                null,
                "Roslyn project load reported MSBuild workspace failure(s): " +
                string.Join(" ", workspaceFailures.Take(3)));
        }

        var document = project?.Documents.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.FilePath) &&
            string.Equals(
                Path.GetFullPath(x.FilePath),
                Path.GetFullPath(context.TestFilePath),
                StringComparison.OrdinalIgnoreCase));

        return new TestDocumentLoadResult(document, null);
    }

    private static async Task<RoslynGeneratedTestDiagnosticSnapshot> CaptureDocumentDiagnosticsAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxTree == null || semanticModel == null)
            return RoslynGeneratedTestDiagnosticSnapshot.Skip("Roslyn could not produce a syntax tree or semantic model.");

        var diagnostics = syntaxTree.GetDiagnostics(cancellationToken)
            .Concat(semanticModel.GetDiagnostics(cancellationToken: cancellationToken))
            .Where(x => x.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(ToSnapshot)
            .DistinctBy(ToDiagnosticKey)
            .OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.StartLine)
            .ThenBy(x => x.StartColumn)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();

        return new RoslynGeneratedTestDiagnosticSnapshot
        {
            Captured = true,
            DocumentPath = document.FilePath,
            Diagnostics = diagnostics
        };
    }

    private static IReadOnlyList<RoslynDiagnosticSnapshot> DiffDiagnostics(
        IReadOnlyList<RoslynDiagnosticSnapshot> before,
        IReadOnlyList<RoslynDiagnosticSnapshot> after)
    {
        var beforeCounts = before
            .GroupBy(ToStableDiagnosticKey, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var result = new List<RoslynDiagnosticSnapshot>();

        foreach (var diagnostic in after)
        {
            var key = ToStableDiagnosticKey(diagnostic);
            if (beforeCounts.TryGetValue(key, out var count) && count > 0)
            {
                beforeCounts[key] = count - 1;
                continue;
            }

            result.Add(diagnostic);
        }

        return result;
    }

    private static IReadOnlyList<RoslynDiagnosticSnapshot> FilterInheritedTestFrameworkResolutionDiagnostics(
        IReadOnlyList<RoslynDiagnosticSnapshot> before,
        IReadOnlyList<RoslynDiagnosticSnapshot> newDiagnostics)
    {
        if (!before.Any(IsTestFrameworkResolutionDiagnostic))
            return newDiagnostics;

        return newDiagnostics
            .Where(x => !IsTestFrameworkResolutionDiagnostic(x))
            .ToList();
    }

    private static bool IsTestFrameworkResolutionDiagnostic(RoslynDiagnosticSnapshot diagnostic)
    {
        if (diagnostic.Id is not ("CS0103" or "CS0246"))
            return false;

        var symbolName = ExtractQuotedSymbolName(diagnostic.Message);
        if (string.IsNullOrWhiteSpace(symbolName))
            return false;

        return IsKnownTestFrameworkSymbol(symbolName);
    }

    private static string? ExtractQuotedSymbolName(string message)
    {
        var start = message.IndexOf('\'');
        if (start < 0)
            return null;

        var end = message.IndexOf('\'', start + 1);
        if (end <= start + 1)
            return null;

        return message.Substring(start + 1, end - start - 1);
    }

    private static bool IsKnownTestFrameworkSymbol(string symbolName)
    {
        var normalized = symbolName.EndsWith("Attribute", StringComparison.Ordinal)
            ? symbolName[..^"Attribute".Length]
            : symbolName;

        return normalized is
            "Assert" or
            "Fact" or
            "Theory" or
            "InlineData" or
            "MemberData" or
            "ClassData" or
            "Trait" or
            "Test" or
            "TestCase" or
            "TestCaseSource" or
            "TestMethod" or
            "DataTestMethod" or
            "DataRow" or
            "TestSubject";
    }

    private static RoslynDiagnosticSnapshot ToSnapshot(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        return new RoslynDiagnosticSnapshot
        {
            Id = diagnostic.Id,
            Severity = diagnostic.Severity.ToString(),
            Message = diagnostic.GetMessage(),
            FilePath = string.IsNullOrWhiteSpace(span.Path) ? null : span.Path,
            StartLine = span.StartLinePosition.Line,
            StartColumn = span.StartLinePosition.Character,
            EndLine = span.EndLinePosition.Line,
            EndColumn = span.EndLinePosition.Character
        };
    }

    private static string ToDiagnosticKey(RoslynDiagnosticSnapshot diagnostic)
    {
        return string.Join(
            "|",
            diagnostic.Id,
            diagnostic.Severity,
            diagnostic.FilePath ?? string.Empty,
            diagnostic.StartLine,
            diagnostic.StartColumn,
            diagnostic.Message);
    }

    private static string ToStableDiagnosticKey(RoslynDiagnosticSnapshot diagnostic)
    {
        return string.Join(
            "|",
            diagnostic.Id,
            diagnostic.Severity,
            diagnostic.FilePath ?? string.Empty,
            diagnostic.Message);
    }

    private sealed record TestDocumentLoadResult(Document? Document, string? SkipReason);
}
