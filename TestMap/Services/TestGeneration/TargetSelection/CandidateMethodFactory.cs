using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.RiskScoring;

namespace TestMap.Services.TestGeneration.TargetSelection;

internal static class CandidateMethodFactory
{
    public static CandidateMethod Create(
        CandidateSelectionRow row,
        DateTime selectionTime,
        MethodRiskScore? riskScore = null,
        MetricDrivenCandidateScore? metricScore = null)
    {
        return new CandidateMethod
        {
            MemberId = row.Id,
            MethodName = row.Name,
            SourceCode = row.FullString,
            Signature = ExtractMethodSignature(row.FullString, row.Name),
            BaselineCoverage = row.LineRate,
            ComplexityScore = row.Complexity,
            SelectionTime = selectionTime,
            RiskScore = riskScore?.RiskScore,
            RiskFactorScores = riskScore?.FactorScores ?? new Dictionary<RiskFactorKind, double>(),
            RiskWeights = riskScore?.Weights ?? new Dictionary<RiskFactorKind, double>(),
            RiskSelectionReason = riskScore?.SelectionReason ?? string.Empty,
            MetricDrivenScore = metricScore?.Score,
            ExpectedMetricDelta = metricScore?.ExpectedMetricDelta,
            MetricConfidence = metricScore?.Confidence,
            MetricFeasibility = metricScore?.Feasibility,
            MetricEstimatedCost = metricScore?.EstimatedCost,
            MetricGuardrailStatus = metricScore?.GuardrailStatus ?? string.Empty,
            MetricSelectionReason = metricScore?.Evidence ?? string.Empty
        };
    }

    private static string ExtractMethodSignature(string sourceCode, string methodName)
    {
        var index = sourceCode.IndexOf(methodName, StringComparison.Ordinal);
        if (index < 0) return methodName;

        var openParen = sourceCode.IndexOf('(', index);
        if (openParen < 0) return methodName;

        var lineStart = sourceCode.LastIndexOf('\n', openParen);
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var closeParen = sourceCode.IndexOf(')', openParen);
        if (closeParen < 0) return sourceCode[lineStart..openParen].Trim();

        return sourceCode[lineStart..Math.Min(sourceCode.Length, closeParen + 1)].Trim();
    }
}