using System.Diagnostics;
using LibGit2Sharp;
using SharpToken;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Database;
using TestMap.Models.Results;
using TestMap.Services.Database;
using TestMap.Services.Testing;
using TestMap.Services.Testing.Providers;

namespace TestMap.Services.Evaluation;

public class FullAnalysisService : IFullAnalysisService
{
    private readonly ProjectModel _projectModel;
    private readonly TestMapConfig _testMapConfig;
    private readonly SqliteDatabaseService _sqliteDatabaseService;
    public readonly BuildTestService _buildTestService;


    public FullAnalysisService(
        ProjectModel project,
        TestMapConfig config,
        SqliteDatabaseService sqliteDatabaseService,
        BuildTestService buildTestService)
    {
        _projectModel = project;
        _testMapConfig = config;
        _sqliteDatabaseService = sqliteDatabaseService;
        _buildTestService = buildTestService;
    }
    public async Task FullAnalysisAsync()
    {
        // get methods without full coverage
        var methodResults = await _sqliteDatabaseService.FindMethodsWithLowCoverage();
        var rng = Random.Shared;
        
        if (methodResults.Count <= 3)
        {
            await RunAnalysis(methodResults, "pass@1", 1);
            await RunAnalysis(methodResults, "pass@5", 5);
        }
        else
        {
            var randomThree = methodResults
                .OrderBy(_ => rng.Next())
                .Take(3)
                .ToList();
            await RunAnalysis(randomThree, "pass@1", 1);
            await RunAnalysis(randomThree, "pass@5", 5);
        }
        
    }

    private async Task RunAnalysis(List<CoverageMethodResult> methods, string strategy, int retries)
    {

        var testGenerator = new TestGenerator(_testMapConfig);

        foreach (var methodResult in methods)
        {
            var candidateTest = "";
            for (var attempt = 1; attempt <= retries; attempt++)
            {
                _projectModel.Logger?.Information($"[Attempt {attempt}] Generating test for {methodResult.MethodName}");
                var prompt = "";

                if (attempt == 1)
                    prompt = testGenerator.CreateTestPrompt(
                        methodResult.MethodBody,
                        methodResult.TestMethodBody,
                        methodResult.TestFramework,
                        methodResult.TestDependencies
                    );

                // if retrying, append last logs
                if (attempt > 1)
                {
                    var logs = "";
                    if (_buildTestService.LatestLogPath != null)
                        logs = await File.ReadAllTextAsync(_buildTestService.LatestLogPath);
                    prompt = testGenerator.CreateRepairTestPrompt(
                        methodResult.MethodBody,
                        candidateTest,
                        methodResult.TestFramework,
                        methodResult.TestDependencies,
                        logs
                    );
                }

                // generate & insert test
                var stopwatch = Stopwatch.StartNew();
                string rawTest = "";
                try
                {
                    rawTest = await testGenerator.CreateTest(prompt);
                }
                catch (Exception ex)
                {
                    _projectModel.Logger?.Error($"Failed to generate test: {ex.Message}");
                    continue;
                }
                stopwatch.Stop();
                double generationTime = stopwatch.Elapsed.TotalSeconds;
                _projectModel.Logger?.Information($"Test generation took {generationTime} seconds.");
                var encoding = GptEncoding.GetEncoding("cl100k_base");
                int tokens = encoding.Encode(prompt).Count;
                _projectModel.Logger?.Information($"Tokens: {tokens}");

                var test = rawTest.Split("```")[1].Replace("csharp", "");

                candidateTest = test;

                var testMethodName = Utilities.Utilities.ExtractTestMethodName(test) ?? "";

                if (string.IsNullOrEmpty(test) || string.IsNullOrEmpty(testMethodName))
                {
                    _projectModel.Logger?.Warning("Failed to generate test.");
                    continue;
                }

                Utilities.Utilities.InsertTestIntoFile(methodResult.TestFilePath, test);

                // run tests (in-memory results)
                 var runResult =
                    await _buildTestService.BuildTestAsync([methodResult.SolutionFilePath], false, testMethodName);
                 
                 // check failures and coverage improvement
                 var failedTests = runResult.Results.Where(r => r.Outcome != "Passed").ToList();
                 var baseline = methodResult.LineRate;

                var runId = await _sqliteDatabaseService.TestRunRepository.GetTestRunId(runResult.RunId);
                
                if (runId != 0)
                {
                    var genTest = new GenerateTestMethod()
                    {
                        SourceMethodId = methodResult.MethodId,
                        TestMethodId = methodResult.TestMethodId,
                        FilePath = methodResult.TestFilePath,
                        GeneratedBody = test,
                        GenerationDuration = generationTime,
                        Model = _testMapConfig.Generation.Model,
                        Provider = _testMapConfig.Generation.Provider,
                        TokenCount = tokens,
                        Strategy = strategy,
                        TestRunId = runId
                    };
                
                    // insert results into DB
                    await _sqliteDatabaseService.GeneratedTestRepository.InsertGeneratedTest(genTest);
                }
                else
                {
                    _projectModel.Logger?.Warning($"Failed to get test run id {runResult.RunId}.");
                }

                RollbackRepo();
                
                // assuming everything goes right
                if (runResult is GeneratedTestRunResult gen)
                    if (!failedTests.Any() && gen.MethodCoverage >= baseline)
                        // if no failures and coverage improved, insert into DB
                        // move to next method
                        break;
            }
        }
    }

    private void RollbackRepo()
    {
        using var repo = new Repository(_projectModel.DirectoryPath);

        // Hard reset to HEAD (clean working tree, discard changes)
        var headCommit = repo.Head.Tip;
        repo.Reset(ResetMode.Hard, headCommit);

        // Remove untracked files if needed
        var status = repo.RetrieveStatus(new StatusOptions());
        foreach (var untracked in status.Untracked)
        {
            var path = Path.Combine(_projectModel.DirectoryPath, untracked.FilePath);
            if (File.Exists(path)) File.Delete(path);
        }

        _projectModel.Logger?.Information("Repository rolled back to last commit.");
    }
}