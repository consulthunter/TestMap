using System.Diagnostics;
using LibGit2Sharp;
using SharpToken;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Results;
using TestMap.App;
using TestMap.Services.Database;
using TestMap.Services.Testing.Providers;

namespace TestMap.Services.Testing;

public class GenerateTestService : IGenerateTestService
{
    private readonly ProjectContext _context;
    private readonly TestMapConfig _testMapConfig;
    private readonly SqliteDatabaseService _sqliteDatabaseService;
    public readonly BuildTestService _buildTestService;

    public GenerateTestService(
        ProjectContext context,
        TestMapConfig config,
        SqliteDatabaseService sqliteDatabaseService,
        BuildTestService buildTestService)
    {
        _context = context;
        _testMapConfig = config;
        _sqliteDatabaseService = sqliteDatabaseService;
        _buildTestService = buildTestService;
    }

    public async Task GenerateTestAsync()
    {
        var methods = await _sqliteDatabaseService.FindMethodsWithLowCoverage();
        var testGenerator = new TestGenerator(_testMapConfig);

        foreach (var methodResult in methods)
        {
            var candidateTest = "";
            for (var attempt = 1; attempt <= _testMapConfig.Generation.MaxRetries; attempt++)
            {
                _context.Project.Logger?.Information($"[Attempt {attempt}] Generating test for {methodResult.MethodName}");
                var prompt = "";

                if (attempt == 1)
                    prompt = testGenerator.CreateTestPrompt(
                        methodResult.MethodBody,
                        methodResult.TestMethodBody,
                        methodResult.TestClassBody,
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
                        methodResult.TestClassBody,
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
                    
                    if (string.IsNullOrEmpty(rawTest)) continue;
                }
                catch (Exception ex)
                {
                    _context.Project.Logger?.Error($"Failed to generate test: {ex.Message}");
                    continue;
                }

                stopwatch.Stop();
                double generationTime = stopwatch.Elapsed.TotalSeconds;
                _context.Project.Logger?.Information($"Test generation took {generationTime} seconds.");
                var encoding = GptEncoding.GetEncoding("cl100k_base");
                int tokens = encoding.Encode(prompt).Count;
                _context.Project.Logger?.Information($"Tokens: {tokens}");

                var test = rawTest.Split("```")[1].Replace("csharp", "");

                candidateTest = test;

                var testMethodName = Utilities.Utilities.ExtractTestMethodName(test) ?? "";
                
                var methodModel = new MethodModel(methodResult.TestClassId,
                    Guid.NewGuid().ToString(),
                    testMethodName, "", new List<string>(),
                    new List<string>(), test, "", true, true, 
                    methodResult.TestFramework,
                    new Location(0, 0, 0, 0)
                    );
                
                // insert test method into DB
                await _sqliteDatabaseService.MethodRepository.InsertMethodsGetId(methodModel);
                

                if (string.IsNullOrEmpty(test) || string.IsNullOrEmpty(testMethodName))
                {
                    _context.Project.Logger?.Warning("Failed to generate test.");
                    continue;
                }

                Utilities.Utilities.InsertTestIntoFile(methodResult.TestClassName, methodResult.TestFilePath, test);

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
                        GenTestMethodId = methodModel.Id,
                        FilePath = methodResult.TestFilePath,
                        GenerationDuration = generationTime,
                        Model = _testMapConfig.Generation.Model,
                        Provider = _testMapConfig.Generation.Provider,
                        TokenCount = tokens,
                        Strategy = "GenerateTest",
                        TestRunId = runId
                    };

                    // insert results into DB
                    await _sqliteDatabaseService.GeneratedTestRepository.InsertGeneratedTest(genTest);
                }
                else
                {
                    _context.Project.Logger?.Warning($"Failed to get test run id {runResult.RunId}.");
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
        using var repo = new Repository(_context.Project.DirectoryPath);

        // Hard reset to HEAD (clean working tree, discard changes)
        var headCommit = repo.Head.Tip;
        repo.Reset(ResetMode.Hard, headCommit);

        // Remove untracked files if needed
        var status = repo.RetrieveStatus(new StatusOptions());
        foreach (var untracked in status.Untracked)
        {
            var path = Path.Combine(_context.Project.DirectoryPath, untracked.FilePath);
            if (File.Exists(path)) File.Delete(path);
        }

        _context.Project.Logger?.Information("Repository rolled back to last commit.");
    }
}