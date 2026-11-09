namespace TestMap.Models.Coverage;

public sealed record CoverageReportScope(
    string ScopeKind,
    string ReportRole,
    int? ExperimentRunId,
    string SourceProjectPath,
    string TestProjectPath,
    string TargetFramework);
