using TestMap.Execution;
using TestMap.Execution.Steps;

namespace TestMap.Runs;

/// <summary>
/// Pipeline for running AI provider comparison experiments.
/// Executes: Clone → Load DB → Extract → Insert → Analyze → Build → Map → Run Experiment
/// </summary>
public class ExperimentRun : IPipelineRun
{
    private readonly CloneRepoStep _cloneRepoStep;
    private readonly LoadDatabaseStep _loadDatabaseStep;
    private readonly ExtractInfoStep _extractInfoStep;
    private readonly InsertProjectInfoStep _insertProjectInfoStep;
    private readonly AnalyzeProjectStep _analyzeProjectStep;
    private readonly CollectCodeMetricsStep _collectCodeMetricsStep;
    private readonly EnrichTestMetadataStep _enrichTestMetadataStep;
    private readonly CollectTestSmellsStep _collectTestSmellsStep;
    private readonly BuildTestStep _buildTestStep;
    private readonly WriteCollectTestsResultStep _writeCollectTestsResultStep;
    private readonly RunExperimentStep _runExperimentStep;

    public ExperimentRun(
        CloneRepoStep cloneRepoStep,
        LoadDatabaseStep loadDatabaseStep,
        ExtractInfoStep extractInfoStep,
        InsertProjectInfoStep insertProjectInfoStep,
        AnalyzeProjectStep analyzeProjectStep,
        CollectCodeMetricsStep collectCodeMetricsStep,
        EnrichTestMetadataStep enrichTestMetadataStep,
        CollectTestSmellsStep collectTestSmellsStep,
        BuildTestStep buildTestStep,
        WriteCollectTestsResultStep writeCollectTestsResultStep,
        RunExperimentStep runExperimentStep)
    {
        _cloneRepoStep = cloneRepoStep;
        _loadDatabaseStep = loadDatabaseStep;
        _extractInfoStep = extractInfoStep;
        _insertProjectInfoStep = insertProjectInfoStep;
        _analyzeProjectStep = analyzeProjectStep;
        _collectCodeMetricsStep = collectCodeMetricsStep;
        _enrichTestMetadataStep = enrichTestMetadataStep;
        _collectTestSmellsStep = collectTestSmellsStep;
        _buildTestStep = buildTestStep;
        _writeCollectTestsResultStep = writeCollectTestsResultStep;
        _runExperimentStep = runExperimentStep;
    }

    public RunPipeline CreatePipeline()
    {
        return new RunPipeline([
            _cloneRepoStep,
            _loadDatabaseStep,
            _extractInfoStep,
            _insertProjectInfoStep,
            _analyzeProjectStep,
            _collectCodeMetricsStep,
            _enrichTestMetadataStep,
            _collectTestSmellsStep,
            _buildTestStep,
            _writeCollectTestsResultStep,
            _runExperimentStep
        ]);
    }
}
