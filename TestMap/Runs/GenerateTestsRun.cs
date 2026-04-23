using TestMap.Execution;
using TestMap.Execution.Steps;

namespace TestMap.Runs;

public class GenerateTestsRun : IPipelineRun
{
    private readonly CloneRepoStep _cloneRepoStep;
    private readonly LoadDatabaseStep _loadDatabaseStep;
    private readonly ExtractInfoStep _extractInfoStep;
    private readonly InsertProjectInfoStep _insertProjectInfoStep;
    private readonly AnalyzeProjectStep _analyzeProjectStep;
    private readonly CollectCodeMetricsStep _collectCodeMetricsStep;
    private readonly BuildTestStep _buildTestStep;
    private readonly GenerateTestsStep _generateTestsStep;
    public GenerateTestsRun(
        CloneRepoStep cloneRepoStep,
        LoadDatabaseStep loadDatabaseStep,
        ExtractInfoStep extractInfoStep,
        InsertProjectInfoStep insertProjectInfoStep,
        AnalyzeProjectStep analyzeProjectStep,
        CollectCodeMetricsStep collectCodeMetricsStep,
        BuildTestStep buildTestStep,
        GenerateTestsStep generateTestsStep
        )
    {
        _cloneRepoStep = cloneRepoStep;
        _loadDatabaseStep = loadDatabaseStep;
        _extractInfoStep = extractInfoStep;
        _insertProjectInfoStep = insertProjectInfoStep;
        _analyzeProjectStep = analyzeProjectStep;
        _collectCodeMetricsStep = collectCodeMetricsStep;
        _buildTestStep = buildTestStep;
        _generateTestsStep = generateTestsStep;
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
            _buildTestStep,
            _generateTestsStep
        ]);
    }
}
