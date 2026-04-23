using System.Xml.Linq;
using TestMap.App;
using TestMap.Models.Results;

namespace TestMap.Services.CollectInformation;

public class CollectTestResultsService(ProjectContext context)
{
    public Task<(List<TestResultModel> Results, string Raw)> CollectAsync(string runId, string runDate)
    {
        var results = new List<TestResultModel>();
        var trxDir = Path.Combine(context.Project.DirectoryPath!, "coverage");

        if (!Directory.Exists(trxDir))
        {
            context.Project.Logger?.Warning($"TRX results directory not found: {trxDir}");
            return Task.FromResult((results, string.Empty));
        }

        var rawResults = new List<string>();
        var trxFiles = Directory.GetFiles(trxDir, "*.trx");

        foreach (var trxFile in trxFiles)
        {
            context.Project.Logger?.Information($"Parsing TRX file: {trxFile}");
            var (fileResults, raw) = ParseTrxFile(trxFile, runId, runDate);
            results.AddRange(fileResults);

            if (!string.IsNullOrWhiteSpace(raw))
            {
                rawResults.Add(raw);
            }
        }

        return Task.FromResult((results, string.Join(Environment.NewLine, rawResults)));
    }

    private static (List<TestResultModel> Results, string Raw) ParseTrxFile(string trxFilePath, string runId, string runDate)
    {
        var results = new List<TestResultModel>();
        var doc = XDocument.Load(trxFilePath);
        var ns = doc.Root?.Name.Namespace ?? "";

        var unitTestResults = doc.Descendants(ns + "UnitTestResult");

        foreach (var result in unitTestResults)
        {
            var testName = (string?)result.Attribute("testName") ?? "";
            var outcome = (string?)result.Attribute("outcome") ?? "";
            var durationStr = (string?)result.Attribute("duration") ?? "00:00:00";
            TimeSpan.TryParse(durationStr, out var duration);

            var errorMessage = result
                .Element(ns + "Output")
                ?.Element(ns + "ErrorInfo")
                ?.Element(ns + "Message")
                ?.Value;

            results.Add(new TestResultModel
            {
                RunId = runId,
                RunDate = runDate,
                TestName = testName,
                Outcome = outcome,
                Duration = duration,
                ErrorMessage = errorMessage
            });
        }

        return (results, doc.ToString());
    }
}
