using TestMap.Execution;
using TestMap.Execution.Steps;

namespace TestMap.Runs;

public class StaticAnalysisRun : IPipelineRun
{
    private readonly CloneRepoStep _cloneRepoStep;
    private readonly LoadDatabaseStep _loadDatabaseStep;
    private readonly ExtractInfoStep _extractInfoStep;
    private readonly InsertProjectInfoStep _insertProjectInfoStep;
    private readonly AnalyzeProjectStep _analyzeProjectStep;
    private readonly CollectCodeMetricsStep _collectCodeMetricsStep;
    private readonly EnrichTestMetadataStep _enrichTestMetadataStep;
    private readonly CollectTestSmellsStep _collectTestSmellsStep;

    public StaticAnalysisRun(
        CloneRepoStep cloneRepoStep,
        LoadDatabaseStep loadDatabaseStep,
        ExtractInfoStep extractInfoStep,
        InsertProjectInfoStep insertProjectInfoStep,
        AnalyzeProjectStep analyzeProjectStep,
        CollectCodeMetricsStep collectCodeMetricsStep,
        EnrichTestMetadataStep enrichTestMetadataStep,
        CollectTestSmellsStep collectTestSmellsStep)
    {
        _cloneRepoStep = cloneRepoStep;
        _loadDatabaseStep = loadDatabaseStep;
        _extractInfoStep = extractInfoStep;
        _insertProjectInfoStep = insertProjectInfoStep;
        _analyzeProjectStep = analyzeProjectStep;
        _collectCodeMetricsStep = collectCodeMetricsStep;
        _enrichTestMetadataStep = enrichTestMetadataStep;
        _collectTestSmellsStep = collectTestSmellsStep;
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
            _collectTestSmellsStep
        ]);
    }
}