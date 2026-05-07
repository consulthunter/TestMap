using TestMap.Models.Experiment;

namespace TestMap.Services.Experiment.Reporting;

public interface IExperimentResultsWriter
{
    Task WriteAsync(
        ExperimentRun experimentRun,
        IReadOnlyList<ExperimentResultFileRow> rows,
        CancellationToken cancellationToken = default);

    Task AppendAsync(
        ExperimentRun experimentRun,
        ExperimentResultFileRow row,
        CancellationToken cancellationToken = default);
}
