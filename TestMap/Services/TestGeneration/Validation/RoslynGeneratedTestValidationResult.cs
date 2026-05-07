namespace TestMap.Services.TestGeneration.Validation;

public sealed class RoslynGeneratedTestValidationResult
{
    public bool Succeeded { get; init; }
    public bool Skipped { get; init; }
    public string? SkipReason { get; init; }
    public string? FailureSummary { get; init; }
    public RoslynGeneratedTestDiagnosticSnapshot Before { get; init; } = RoslynGeneratedTestDiagnosticSnapshot.Skipped;
    public RoslynGeneratedTestDiagnosticSnapshot After { get; init; } = RoslynGeneratedTestDiagnosticSnapshot.Skipped;
    public IReadOnlyList<RoslynDiagnosticSnapshot> NewDiagnostics { get; init; } = [];

    public static RoslynGeneratedTestValidationResult Skip(string reason)
    {
        return new RoslynGeneratedTestValidationResult
        {
            Succeeded = true,
            Skipped = true,
            SkipReason = reason,
            Before = RoslynGeneratedTestDiagnosticSnapshot.Skip(reason),
            After = RoslynGeneratedTestDiagnosticSnapshot.Skip(reason)
        };
    }
}

public sealed class RoslynGeneratedTestDiagnosticSnapshot
{
    public static RoslynGeneratedTestDiagnosticSnapshot Skipped { get; } = Skip("Not captured.");

    public bool Captured { get; init; }
    public string? SkipReason { get; init; }
    public string? DocumentPath { get; init; }
    public IReadOnlyList<RoslynDiagnosticSnapshot> Diagnostics { get; init; } = [];

    public static RoslynGeneratedTestDiagnosticSnapshot Skip(string reason)
    {
        return new RoslynGeneratedTestDiagnosticSnapshot
        {
            Captured = false,
            SkipReason = reason
        };
    }
}

public sealed class RoslynDiagnosticSnapshot
{
    public required string Id { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public string? FilePath { get; init; }
    public int StartLine { get; init; }
    public int StartColumn { get; init; }
    public int EndLine { get; init; }
    public int EndColumn { get; init; }
}

