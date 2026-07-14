using TestMap.Models.Coverage;
using TestMap.Models.Testing;

namespace TestMap.Services.TestExecution;

public static class CoverageTargetResolver
{
    public static CoverageTargetResolution Resolve(
        CoverageReportModel? report,
        TargetMemberDescriptor? target)
    {
        if (target == null)
            return CoverageTargetResolution.Missing("NotRequested", "No target member was supplied.");
        if (report == null)
            return CoverageTargetResolution.Missing("MissingReport", "The test run produced no coverage report.");

        var objects = report.Packages
            .SelectMany(x => x.Classes)
            .Where(x => MatchesFile(x.Filename, target.SourceFilePath))
            .Where(x => MatchesType(x.Name, target.ContainingType))
            .ToList();

        if (objects.Count == 0)
            return CoverageTargetResolution.Missing(
                "TargetTypeNotFound",
                $"No coverage type matched '{target.ContainingType}' in '{target.SourceFilePath}'.");

        var methods = objects
            .SelectMany(x => x.Methods)
            .Where(x => NormalizeMethodName(x.Name)
                .Equals(target.MethodName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (methods.Count == 0)
            return CoverageTargetResolution.Missing(
                "TargetMethodNotFound",
                $"No coverage method matched '{target.MethodName}'.");

        var byLine = methods.Where(x => OverlapsTargetLines(x, target)).ToList();
        if (byLine.Count == 1)
            return CoverageTargetResolution.Resolved(byLine[0].LineRate, "SourceLine");
        if (byLine.Count > 1)
            methods = byLine;

        var targetParameterCount = CountParameters(target.MethodSignature);
        var bySignature = methods
            .Where(x => CountParameters(x.Signature) == targetParameterCount)
            .ToList();
        if (bySignature.Count == 1)
            return CoverageTargetResolution.Resolved(bySignature[0].LineRate, "Signature");

        if (methods.Count == 1)
            return CoverageTargetResolution.Resolved(methods[0].LineRate, "UniqueName");

        return CoverageTargetResolution.Missing(
            "AmbiguousTargetMethod",
            $"Coverage contained {methods.Count} possible matches for '{target.MethodName}'.");
    }

    private static bool MatchesFile(string coverageFile, string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(coverageFile) || string.IsNullOrWhiteSpace(sourceFile))
            return true;

        var coverage = NormalizePath(coverageFile);
        var source = NormalizePath(sourceFile);
        return source.EndsWith(coverage, StringComparison.OrdinalIgnoreCase) ||
               coverage.EndsWith(source, StringComparison.OrdinalIgnoreCase) ||
               Path.GetFileName(source).Equals(Path.GetFileName(coverage), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesType(string coverageType, string containingType)
    {
        return CoverageTypeName.Matches(coverageType, containingType);
    }

    private static bool OverlapsTargetLines(MemberCoverageModel method, TargetMemberDescriptor target)
    {
        if (target.StartLine < 0 || target.EndLine < target.StartLine || method.Lines.Count == 0)
            return false;

        // Roslyn source locations are zero-based; Cobertura line numbers are one-based.
        var coverageStartLine = target.StartLine + 1;
        var coverageEndLine = target.EndLine + 1;
        return method.Lines.Any(x => x.Number >= coverageStartLine && x.Number <= coverageEndLine);
    }

    private static string NormalizeMethodName(string value)
    {
        var name = value.Trim();
        var parameters = name.IndexOf('(');
        if (parameters >= 0) name = name[..parameters];
        var generic = name.IndexOf('<');
        if (generic >= 0) name = name[..generic];
        return name;
    }

    private static int CountParameters(string signature)
    {
        var start = signature.IndexOf('(');
        var end = signature.LastIndexOf(')');
        if (start < 0 || end <= start + 1) return 0;

        var content = signature[(start + 1)..end].Trim();
        if (content.Length == 0) return 0;

        var depth = 0;
        var count = 1;
        foreach (var character in content)
        {
            if (character is '<' or '[' or '(') depth++;
            else if (character is '>' or ']' or ')') depth--;
            else if (character == ',' && depth == 0) count++;
        }

        return count;
    }

    private static string NormalizePath(string value) => value.Replace('\\', '/').Trim();
}

public sealed record CoverageTargetResolution(
    double? LineRate,
    string Status,
    string Reason,
    string MatchKind)
{
    public static CoverageTargetResolution Resolved(double lineRate, string matchKind) =>
        new(lineRate, "Resolved", string.Empty, matchKind);

    public static CoverageTargetResolution Missing(string status, string reason) =>
        new(null, status, reason, string.Empty);
}
