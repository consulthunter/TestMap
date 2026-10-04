using System.Globalization;
using System.Text.RegularExpressions;
using TestMap.Models.Coverage;

namespace TestMap.Persistence.Ef.Mappings;

public static partial class CoverageCounterCalculator
{
    public static CoverageCounterResult Calculate(IReadOnlyCollection<LineCoverageModel> lines)
    {
        if (lines.Count == 0) return CoverageCounterResult.Unavailable;

        var distinctLines = lines
            .Where(line => line.Number > 0)
            .GroupBy(line => line.Number)
            .ToList();
        var linesValid = distinctLines.Count;
        var linesCovered = distinctLines.Count(group => group.Any(line => line.Hits > 0));
        var branchesCovered = 0;
        var branchesValid = 0;
        var branchesAvailable = true;

        foreach (var lineGroup in distinctLines)
        {
            var branchDetails = lineGroup.Where(IsBranchLine).ToList();
            if (branchDetails.Count == 0) continue;

            var candidates = branchDetails
                .Select(ParseBranchCounts)
                .Where(result => result.HasValue)
                .Select(result => result!.Value)
                .ToList();
            if (candidates.Count == 0)
            {
                branchesAvailable = false;
                continue;
            }

            var best = candidates.OrderByDescending(x => x.Valid).ThenByDescending(x => x.Covered).First();
            if (best.Covered > best.Valid)
                return new CoverageCounterResult(
                    linesCovered, linesValid, 0, 0, true, false, false,
                    $"Covered branch count {best.Covered} exceeds valid branch count {best.Valid} on line {lineGroup.Key}.");
            branchesCovered += best.Covered;
            branchesValid += best.Valid;
        }

        return new CoverageCounterResult(
            linesCovered,
            linesValid,
            branchesAvailable ? branchesCovered : 0,
            branchesAvailable ? branchesValid : 0,
            true,
            branchesAvailable,
            true,
            string.Empty);
    }

    private static (int Covered, int Valid)? ParseBranchCounts(LineCoverageModel line)
    {
        var fraction = BranchFractionRegex().Match(line.ConditionCoverage ?? string.Empty);
        if (fraction.Success &&
            int.TryParse(fraction.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var covered) &&
            int.TryParse(fraction.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var valid))
            return (covered, valid);

        if (line.Conditions.Count == 0) return null;
        var parsed = line.Conditions
            .Select(condition => ParsePercentage(condition.Coverage))
            .ToList();
        if (parsed.Any(value => !value.HasValue)) return null;
        return (parsed.Count(value => value!.Value > 0), parsed.Count);
    }

    private static double? ParsePercentage(string value)
    {
        var normalized = (value ?? string.Empty).Trim().TrimEnd('%');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static bool IsBranchLine(LineCoverageModel line) =>
        string.Equals(line.Branch, "true", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\((\d+)\s*/\s*(\d+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex BranchFractionRegex();
}

public readonly record struct CoverageCounterResult(
    int LinesCovered,
    int LinesValid,
    int BranchesCovered,
    int BranchesValid,
    bool LineCountsAvailable,
    bool BranchCountsAvailable,
    bool IsValid,
    string ValidationError)
{
    public static CoverageCounterResult Unavailable => new(0, 0, 0, 0, false, false, true, string.Empty);
}
