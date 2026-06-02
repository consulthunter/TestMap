namespace TestMap.Models.MutationTesting;

public sealed record MutationTestingReportScope(
    string ScopeKind,
    bool IsBaseline,
    int? ExperimentRunId = null,
    string SourceProjectPath = "",
    string TestProjectPath = "",
    string TargetFramework = "")
{
    public static MutationTestingReportScope SolutionBaseline() => new("Solution", true);

    public static MutationTestingReportScope SourceProject(
        bool isBaseline,
        int? experimentRunId,
        string? sourceProjectPath,
        string? testProjectPath,
        string? targetFramework)
    {
        return new MutationTestingReportScope(
            "SourceProject",
            isBaseline,
            experimentRunId,
            NormalizePath(sourceProjectPath),
            NormalizePath(testProjectPath),
            targetFramework?.Trim() ?? string.Empty);
    }

    private static string NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
