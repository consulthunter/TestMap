using TestMap.Models.Experiment;

namespace TestMap.Services.Experiment.Reporting;

public interface IAssertionObservationWriter
{
    Task WriteAsync(
        ExperimentRun experimentRun,
        IReadOnlyList<AssertionObservationFileRow> rows,
        CancellationToken cancellationToken = default);

    Task AppendAsync(
        ExperimentRun experimentRun,
        IReadOnlyList<AssertionObservationFileRow> rows,
        CancellationToken cancellationToken = default);
}
