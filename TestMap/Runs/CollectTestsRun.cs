using TestMap.Execution;
using TestMap.Execution.Steps;

namespace TestMap.Runs;

public class CollectTestsRun : IPipelineRun
{
    private readonly CloneRepoStep _cloneRepoStep;
    private readonly LoadDatabaseStep _loadDatabaseStep;
    private readonly ExtractInfoStep _extractInfoStep;
    private readonly InsertProjectInfoStep _insertProjectInfoStep;
    private readonly AnalyzeProjectStep _analyzeProjectStep;
    private readonly BuildTestStep _buildTestStep;
    private readonly MapInfoStep _mapInfoStep;

    public CollectTestsRun(
        CloneRepoStep cloneRepoStep,
        LoadDatabaseStep loadDatabaseStep,
        ExtractInfoStep extractInfoStep,
        InsertProjectInfoStep insertProjectInfoStep,
        AnalyzeProjectStep analyzeProjectStep,
        BuildTestStep buildTestStep,
        MapInfoStep mapInfoStep)
    {
        _cloneRepoStep = cloneRepoStep;
        _loadDatabaseStep = loadDatabaseStep;
        _extractInfoStep = extractInfoStep;
        _insertProjectInfoStep = insertProjectInfoStep;
        _analyzeProjectStep = analyzeProjectStep;
        _buildTestStep = buildTestStep;
        _mapInfoStep = mapInfoStep;
    }

    public RunPipeline CreatePipeline()
    {
        return new RunPipeline([
            _cloneRepoStep,
            _loadDatabaseStep,
            _extractInfoStep,
            _insertProjectInfoStep,
            _analyzeProjectStep,
            _buildTestStep,
            _mapInfoStep
        ]);
    }
}