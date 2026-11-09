using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using TestMap.App;
using TestMap.Models.Coverage;

namespace TestMap.Services.TestExecution.Collection;

public class CollectCoverageResultsService(ProjectContext context)
{
    public async Task<(CoverageReportModel? Report, string RawReport, string NormalizedReport)>
        CollectAsync(string runId)
    {
        var coverageDir = Path.Combine(context.Project.DirectoryPath!, "coverage");
        var rawFile = Path.Combine(coverageDir, $"merged_{runId}_raw.cobertura.xml");
        var normalizedFile = Path.Combine(coverageDir, $"report_{runId}", "Cobertura.xml");
        var mergedNormalizedFile = Path.Combine(coverageDir, $"merged_{runId}.cobertura.xml");
        var sidecarFile = Path.Combine(coverageDir, $"collection_{runId}.json");

        var rawReport = string.Empty;
        var normalizedReport = string.Empty;
        var reportMetadata = new CoverageReportModel
        {
            RunId = runId,
            MeasurementPolicyVersion = CoverageReportModel.CorrectedPolicyVersion,
            HasUsableCoverage = false
        };

        try
        {
            if (!File.Exists(sidecarFile))
            {
                reportMetadata.CollectionStatus = "CollectionFailed";
                reportMetadata.CollectionReason = "Coverage collection sidecar is missing for this run.";
                return (reportMetadata, rawReport, normalizedReport);
            }

            var sidecarJson = await File.ReadAllTextAsync(sidecarFile);
            reportMetadata.CollectionMetadataJson = sidecarJson;
            using var sidecar = JsonDocument.Parse(sidecarJson);
            var sidecarRoot = sidecar.RootElement;
            var sidecarRunId = GetString(sidecarRoot, "runId");
            if (!string.IsNullOrWhiteSpace(sidecarRunId)) reportMetadata.RunId = sidecarRunId;
            reportMetadata.CollectionStatus = GetString(sidecarRoot, "status");
            reportMetadata.CollectionReason = GetString(sidecarRoot, "reason");
            reportMetadata.SuccessfulCollector = GetString(sidecarRoot, "successfulCollector");

            if (File.Exists(rawFile)) rawReport = await File.ReadAllTextAsync(rawFile);

            var effectiveNormalizedFile = File.Exists(normalizedFile)
                ? normalizedFile
                : File.Exists(mergedNormalizedFile)
                    ? mergedNormalizedFile
                    : null;

            if (effectiveNormalizedFile == null)
            {
                if (!File.Exists(rawFile)) context.Project.Logger?.Warning($"Raw coverage file not found: {rawFile}");

                context.Project.Logger?.Warning($"Normalized coverage file not found: {normalizedFile}");
                reportMetadata.CollectionStatus = NormalizeMissingArtifactStatus(reportMetadata.CollectionStatus);
                if (string.IsNullOrWhiteSpace(reportMetadata.CollectionReason))
                    reportMetadata.CollectionReason = "No normalized current-run coverage artifact was produced.";
                return (reportMetadata, rawReport, normalizedReport);
            }

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore
            };

            using var stream = new FileStream(effectiveNormalizedFile, FileMode.Open, FileAccess.Read);
            using var reader = XmlReader.Create(stream, settings);
            var serializer = new XmlSerializer(typeof(CoverageReportModel));
            var report = serializer.Deserialize(reader) as CoverageReportModel ?? new CoverageReportModel();

            normalizedReport = await File.ReadAllTextAsync(effectiveNormalizedFile);
            report.RunId = reportMetadata.RunId;
            report.CollectionMetadataJson = reportMetadata.CollectionMetadataJson;
            report.CollectionReason = reportMetadata.CollectionReason;
            report.SuccessfulCollector = reportMetadata.SuccessfulCollector;
            report.MeasurementPolicyVersion = CoverageReportModel.CorrectedPolicyVersion;
            report.LineCountsAvailable = HasRootAttribute(normalizedReport, "lines-covered") &&
                                         HasRootAttribute(normalizedReport, "lines-valid");
            report.BranchCountsAvailable = HasRootAttribute(normalizedReport, "branches-covered") &&
                                           HasRootAttribute(normalizedReport, "branches-valid");
            report.CollectionStatus = report.Packages.Sum(x => x.Classes.Count) == 0
                ? "ParsedNoData"
                : "PendingAttribution";

            if (!File.Exists(rawFile))
                context.Project.Logger?.Debug(
                    "Raw coverage file was not produced for run {RunId}; using normalized coverage only.", runId);

            context.Project.Logger?.Information("Normalized coverage report loaded successfully.");
            return (report, rawReport, normalizedReport);
        }
        catch (Exception ex)
        {
            context.Project.Logger?.Error($"Error loading coverage report: {ex.Message}");
            reportMetadata.CollectionStatus = "ParseFailed";
            reportMetadata.CollectionReason = ex.Message;
            reportMetadata.HasUsableCoverage = false;
            return (reportMetadata, rawReport, normalizedReport);
        }
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string NormalizeMissingArtifactStatus(string status)
    {
        return status is "ProviderUnavailable" or "CollectionFailed" or "NoArtifact" or "MergeFailed"
            ? status
            : "NoArtifact";
    }

    private static bool HasRootAttribute(string xml, string attributeName)
    {
        using var stringReader = new StringReader(xml);
        using var reader = XmlReader.Create(stringReader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        reader.MoveToContent();
        return reader.GetAttribute(attributeName) != null;
    }
}
