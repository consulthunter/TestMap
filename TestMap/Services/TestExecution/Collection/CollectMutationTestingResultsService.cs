using System.Text.Json;
using TestMap.App;
using TestMap.Models.Results;

namespace TestMap.Services.TestExecution.Collection;

public class CollectMutationTestingResultsService(ProjectContext context)
{
    public async Task<(List<StrykerMutationResults> Reports, string Raw)> CollectAsync(string runId,
        List<string> solutions)
    {
        var reports = new List<StrykerMutationResults>();
        var rawReports = new List<string>();

        foreach (var solution in solutions)
        {
            var solutionName = Path.GetFileNameWithoutExtension(solution);
            var reportDir = $"{solutionName}_{runId}";
            var reportPath = Path.Combine(
                context.Project.DirectoryPath!,
                "mutation",
                reportDir,
                "reports",
                "mutation-report.json");

            try
            {
                if (!File.Exists(reportPath))
                {
                    context.Project.Logger?.Warning($"Mutation report not found: {reportPath}");
                    continue;
                }

                var json = await File.ReadAllTextAsync(reportPath);
                var result = JsonSerializer.Deserialize<StrykerMutationResults>(json);

                if (result != null)
                {
                    reports.Add(result);
                    rawReports.Add(json);
                }

                context.Project.Logger?.Information($"Mutation report loaded successfully from {reportPath}.");
            }
            catch (Exception ex)
            {
                context.Project.Logger?.Error($"Error loading mutation report '{reportPath}': {ex.Message}");
            }
        }

        return (reports, string.Join(Environment.NewLine, rawReports));
    }
}