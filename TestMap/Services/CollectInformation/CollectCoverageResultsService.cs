using System.Xml;
using System.Xml.Serialization;
using TestMap.App;
using TestMap.Models.Coverage;

namespace TestMap.Services.CollectInformation;

public class CollectCoverageResultsService(ProjectContext context)
{
    public async Task<(CoverageReportModel? Report, string RawReport, string NormalizedReport)> CollectAsync(string runId)
    {
        var coverageDir = Path.Combine(context.Project.DirectoryPath!, "coverage");
        var rawFile = Path.Combine(coverageDir, $"merged_{runId}_raw.cobertura.xml");
        var normalizedFile = Path.Combine(coverageDir, $"report_{runId}", "Cobertura.xml");
        var mergedNormalizedFile = Path.Combine(coverageDir, $"merged_{runId}.cobertura.xml");

        string rawReport = string.Empty;
        string normalizedReport = string.Empty;

        try
        {
            if (File.Exists(rawFile))
            {
                rawReport = await File.ReadAllTextAsync(rawFile);
            }

            var effectiveNormalizedFile = File.Exists(normalizedFile)
                ? normalizedFile
                : File.Exists(mergedNormalizedFile)
                    ? mergedNormalizedFile
                    : null;

            if (effectiveNormalizedFile == null)
            {
                if (!File.Exists(rawFile))
                {
                    context.Project.Logger?.Warning($"Raw coverage file not found: {rawFile}");
                }

                context.Project.Logger?.Warning($"Normalized coverage file not found: {normalizedFile}");
                return (new CoverageReportModel(), rawReport, normalizedReport);
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

            if (!File.Exists(rawFile))
            {
                context.Project.Logger?.Debug("Raw coverage file was not produced for run {RunId}; using normalized coverage only.", runId);
            }

            context.Project.Logger?.Information("Normalized coverage report loaded successfully.");
            return (report, rawReport, normalizedReport);
        }
        catch (Exception ex)
        {
            context.Project.Logger?.Error($"Error loading coverage report: {ex.Message}");
            return (new CoverageReportModel(), rawReport, normalizedReport);
        }
    }
}
