using System.Text.RegularExpressions;
using TestMap.Models.Results;

namespace TestMap.Services.TestExecution;

public class CallFailureService
{
    private readonly List<FailureRule> _rules =
    [
        new(
            "infrastructure",
            "docker_engine_failure",
            "Docker failed before the project build or test workflow could start.",
            "Verify the Docker daemon/context is available and that the selected container context is running.",
            0.98,
            "docker",
            [
                "Cannot connect to the Docker daemon",
                "error during connect",
                "The system cannot find the file specified",
                "no such host"
            ]),
        new(
            "restore",
            "package_restore_failure",
            "NuGet package restore failed.",
            "Check package sources, feed credentials, network access, and whether the package/version exists for the selected target framework.",
            0.97,
            "restore",
            [
                "NU1101",
                "NU1102",
                "NU1301",
                "NU1302",
                "NU1303",
                "NU1304",
                "Unable to find package",
                "Unable to load the service index",
                "Response status code does not indicate success: 401",
                "Response status code does not indicate success: 403"
            ]),
        new(
            "build",
            "image_dependency_missing",
            "The container image is missing a required build dependency.",
            "Add the missing tool or runtime to the image. For Mono-based projects, install Mono/MSBuild/NuGet support in the container image.",
            0.95,
            "image",
            [
                "mono: command not found",
                "mono: not found",
                "msbuild: command not found",
                "xbuild: command not found",
                "nuget: command not found",
                "No such file or directory: mono"
            ]),
        new(
            "build",
            "platform_mismatch",
            "The project requires Windows-specific targeting or workloads that are not available in the current container.",
            "Run the project in a Windows container or enable the required Windows targeting packs/workloads for the selected SDK image.",
            0.96,
            "build",
            [
                "NETSDK1100",
                "NETSDK1136",
                "WindowsDesktop",
                "UseWPF",
                "UseWindowsForms",
                "Microsoft.NET.Sdk.WindowsDesktop"
            ]),
        new(
            "build",
            "sdk_or_workload_missing",
            "The required .NET SDK, workload, or imported build targets are missing.",
            "Install the matching SDK/workload in the image or align the image SDK with the project's global.json and imported targets.",
            0.95,
            "build",
            [
                "MSB4019",
                "NETSDK1147",
                "The SDK '",
                "Workload installation failed",
                "was not found. Install the",
                "It was not possible to find any installed .NET SDKs"
            ]),
        new(
            "build",
            "compilation_failure",
            "The project restored, but compilation failed.",
            "Inspect the compiler errors and project references. The source or target framework configuration likely needs to be fixed before tests can run.",
            0.9,
            "build",
            [
                "Build FAILED.",
                ": error CS",
                ": error MSB",
                ": error NU",
                "CSC : error"
            ]),
        new(
            "test",
            "test_execution_failure",
            "The build completed, but test execution failed before producing usable results.",
            "Inspect test host startup, adapter availability, and test framework configuration inside the container.",
            0.88,
            "test",
            [
                "Test Run Aborted",
                "The active test run was aborted",
                "No test is available",
                "testhost",
                "Could not find testhost"
            ]),
        new(
            "coverage",
            "coverage_collection_failure",
            "Coverage collection failed inside the container.",
            "Ensure the configured collector is installed and compatible with the project. For cross-platform runs, prefer the XPlat/Coverlet collector when available.",
            0.9,
            "coverage",
            [
                "Could not find data collector",
                "XPlat Code Coverage",
                "Unable to find a datacollector with friendly name",
                "coverage"
            ])
    ];

    public FailureAnalysisModel? Analyze(string? logs, string? processDiagnostics = null)
    {
        var combined = string.Join(
            Environment.NewLine,
            new[] { logs, processDiagnostics }.Where(x => !string.IsNullOrWhiteSpace(x)));

        if (string.IsNullOrWhiteSpace(combined)) return null;

        foreach (var rule in _rules)
        {
            var matched = rule.Patterns
                .Where(pattern => combined.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matched.Count == 0) continue;

            return new FailureAnalysisModel
            {
                Stage = rule.Stage,
                Category = rule.Category,
                Summary = rule.Summary,
                RemediationSuggestion = rule.Remediation,
                Confidence = rule.Confidence,
                Source = rule.Source,
                MatchedPatterns = matched,
                Evidence = ExtractEvidence(combined, matched[0])
            };
        }

        return new FailureAnalysisModel
        {
            Stage = "unknown",
            Category = "unknown_failure",
            Summary = "The container run failed, but no known failure pattern matched the available diagnostics.",
            RemediationSuggestion =
                "Inspect the stored docker log and add a classifier rule for this failure pattern if it is recurring.",
            Confidence = 0.25,
            Source = "unknown",
            MatchedPatterns = new List<string>(),
            Evidence = ExtractEvidence(combined, "error")
        };
    }

    private static string ExtractEvidence(string content, string pattern)
    {
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var matchedLine = lines.FirstOrDefault(line => line.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(matchedLine)) return matchedLine.Trim();

        var generic = lines.FirstOrDefault(line =>
            Regex.IsMatch(line, "error|failed|abort|exception", RegexOptions.IgnoreCase));
        return generic?.Trim() ?? string.Empty;
    }

    private sealed record FailureRule(
        string Stage,
        string Category,
        string Summary,
        string Remediation,
        double Confidence,
        string Source,
        List<string> Patterns);
}