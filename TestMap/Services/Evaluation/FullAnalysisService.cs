using System.Diagnostics;
using LibGit2Sharp;
using Microsoft.CodeAnalysis.CSharp;
using SharpToken;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Database;
using TestMap.Models.Results;
using TestMap.Services.Database;
using TestMap.Services.Testing;
using TestMap.Services.Testing.Providers;
using TestMap.Services.xNose;

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
            var baseline = methodResult.LineRate;
            var candidateTest = "";

            for (var attempt = 1; attempt <= retries; attempt++)
            {
                _projectModel.Logger?.Information(
                    $"[{strategy}] Attempt {attempt} for {methodResult.MethodName}");

                string prompt;

                if (attempt == 1)
                {
                    prompt = testGenerator.CreateTestPrompt(
                        methodResult.MethodBody,
                        methodResult.TestMethodBody,
                        methodResult.TestClassBody,
                        methodResult.TestFramework,
                        methodResult.TestDependencies);
                }
                else
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
                        logs);
                }

                // --- Generate test ---
                string rawTest;
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    rawTest = await testGenerator.CreateTest(prompt);
                    if (string.IsNullOrWhiteSpace(rawTest))
                        continue;
                }
                catch (Exception ex)
                {
                    _projectModel.Logger?.Error($"Generation failed: {ex.Message}");
                    continue;
                }

                stopwatch.Stop();
                var generationTime = stopwatch.Elapsed.TotalSeconds;

                var encoding = GptEncoding.GetEncoding("cl100k_base");
                var tokens = encoding.Encode(prompt).Count;

                var test = rawTest;
                if (rawTest.Contains("```"))
                {
                    var parts = rawTest.Split("```");
                    if (parts.Length > 1)
                    {
                        test = parts[1].Replace("csharp", "");
                    }
                }
                else
                {
                    _projectModel.Logger?.Warning("No markdown code block delimiters (```) found in rawTest. Using raw string.");
                }
                
                test = test.Trim();
                
                if (string.IsNullOrWhiteSpace(test))
                {
                    _projectModel.Logger?.Warning("Extracted test code is empty.");
                    continue;
                }
                
                candidateTest = test;

                var testMethodName =
                    Utilities.Utilities.ExtractTestMethodName(test) ?? "";

                if (string.IsNullOrEmpty(testMethodName))
                {
                    _projectModel.Logger?.Warning("Failed to extract test name.");
                    continue;
                }

                // --- Insert test into DB and file ---
                var methodModel = new MethodModel(
                    methodResult.TestClassId,
                    Guid.NewGuid().ToString(),
                    testMethodName,
                    "",
                    new List<string>(),
                    new List<string>(),
                    test,
                    "",
                    true,
                    true,
                    methodResult.TestFramework,
                    new Location(0, 0, 0, 0)
                );

                await _sqliteDatabaseService.MethodRepository
                    .InsertMethodsGetId(methodModel);

                Utilities.Utilities.InsertTestIntoFile(
                    methodResult.TestClassName,
                    methodResult.TestFilePath,
                    test);
                
                // --- Run tests ---
                var runResult = await _buildTestService.BuildTestAsync(
                    [methodResult.SolutionFilePath],
                    false,
                    testMethodName);

                // --- Find generated test result ONLY ---
                var generatedTestResult =
                    runResult.Results.FirstOrDefault(r =>
                        r.TestName.Contains(testMethodName));

                var runId = await _sqliteDatabaseService
                    .TestRunRepository
                    .GetTestRunId(runResult.RunId);

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
                        Strategy = strategy,
                        TestRunId = runId
                    };

                    await _sqliteDatabaseService
                        .GeneratedTestRepository
                        .InsertGeneratedTest(genTest);

                    var text = await File.ReadAllTextAsync(methodResult.TestFilePath);
                    var code = CSharpSyntaxTree.ParseText(text);
                    var root = await code.GetRootAsync();

                    var xnose = new xNoseService(
                        _projectModel,
                        _sqliteDatabaseService);

                    xnose.ClassVirtualizationVisitor.Visit(root);
                    await xnose.Analyze(methodResult.SolutionFilePath);
                }

                // --- Evaluate outcome ---
                bool shouldBreak = false;

                if (generatedTestResult == null)
                {
                    _projectModel.Logger?.Warning(
                        $"Generated test did not run: {testMethodName} (suite aborted?)");

                    // Retry allowed
                }
                else
                {
                    _projectModel.Logger?.Information(
                        $"Generated test outcome: {generatedTestResult.Outcome}");

                    if (generatedTestResult.Outcome == "Passed")
                    {
                        if (runResult is GeneratedTestRunResult gen &&
                            gen.MethodCoverage > baseline)
                        {
                            _projectModel.Logger?.Information(
                                $"Coverage improved from {baseline} to {gen.MethodCoverage}");
                        }

                        // Successful run → no need to retry
                        shouldBreak = true;
                    }
                    else if (generatedTestResult.Outcome == "Failed")
                    {
                        _projectModel.Logger?.Warning(
                            $"Generated test failed: {testMethodName}");
                        // Retry allowed
                    }
                }

                RollbackRepo();

                if (shouldBreak)
                    break;
            }
        }
    }

    private void RollbackRepo()
    {
        using var repo = new Repository(_projectModel.DirectoryPath);

        var headCommit = repo.Head.Tip;
        repo.Reset(ResetMode.Hard, headCommit);

        var status = repo.RetrieveStatus(new StatusOptions());

        foreach (var untracked in status.Untracked)
        {
            var path = Path.Combine(
                _projectModel.DirectoryPath,
                untracked.FilePath);

            if (File.Exists(path))
                File.Delete(path);
        }

        _projectModel.Logger?.Information(
            "Repository rolled back to last commit.");
    }
}