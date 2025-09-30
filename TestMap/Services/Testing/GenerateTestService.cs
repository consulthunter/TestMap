using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LibGit2Sharp;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Database;
using TestMap.Services.Database;
using TestMap.Services.Testing.Providers;

namespace TestMap.Services.Testing;

public class GenerateTestService : IGenerateTestService
{
    private readonly ProjectModel _projectModel;
    private readonly TestMapConfig _testMapConfig;
    private readonly SqliteDatabaseService _sqliteDatabaseService;
    public readonly BuildTestService _buildTestService;

    public GenerateTestService(
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

    public async Task GenerateTestAsync()
    {
        // get methods without full coverage
        List<CoverageMethodResult> methodResults = await _sqliteDatabaseService.FindMethodsWithLowCoverage();
        TestGenerator testGenerator = new TestGenerator(_testMapConfig);

        foreach (var methodResult in methodResults)
        {
            string candidateTest = "";
            for (int attempt = 1; attempt <= _testMapConfig.Generation.MaxRetries; attempt++)
            {
                _projectModel.Logger?.Information($"[Attempt {attempt}] Generating test for {methodResult.MethodName}");
                string prompt = "";

                if (attempt == 1)
                {
                    prompt = testGenerator.CreateTestPrompt(
                        methodResult.MethodBody,
                        methodResult.TestMethodBody,
                        methodResult.TestFramework,
                        methodResult.TestDependencies
                    );
                }

                // if retrying, append last logs
                if (attempt > 1)
                {
                    string logs = "";
                    if (_buildTestService.LatestLogPath != null)
                    {
                        logs = await File.ReadAllTextAsync(_buildTestService.LatestLogPath);
                    }
                    prompt = testGenerator.CreateRepairTestPrompt(
                        methodResult.MethodBody,
                        candidateTest,
                        methodResult.TestFramework,
                        methodResult.TestDependencies,
                        logs
                    );
                }

                // generate & insert test
                string rawTest = await testGenerator.CreateTest(prompt);
                string test = rawTest.Split("```")[1].Replace("csharp", "");;
                candidateTest = test;
                string testMethodName = ExtractTestMethodName(test) ?? "";
                
                if (String.IsNullOrEmpty(test) || String.IsNullOrEmpty(testMethodName))
                {
                    _projectModel.Logger?.Warning("Failed to generate test.");
                    continue;
                }
                
                InsertTestIntoFile(methodResult.TestFilePath, test);

                // run tests (in-memory results)
                var runResult = await _buildTestService.RunForGenerationAsync(testMethodName);

                bool rollback = false;

                if (!runResult.Success)
                {
                    rollback = true;
                }
                else
                {
                    // check failures and coverage improvement
                    var failedTests = runResult.Results.Where(r => r.Outcome != "Passed").ToList();
                    double baseline = methodResult.LineRate;

                    if (failedTests.Any() || runResult.MethodCoverage <= baseline)
                        rollback = true;
                }

                if (rollback)
                {
                    RollbackRepo();
                    if (attempt < _testMapConfig.Generation.MaxRetries)
                    {
                        _projectModel.Logger?.Information("Retrying with new generation...");
                        continue;
                    }
                    else
                    {
                        _projectModel.Logger?.Warning("Max retries reached, giving up.");
                    }
                }
                else
                {
                    CommitToBranch(runResult.RunId);
                }

                break; // exit loop after success or final failure
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
        RepositoryStatus status = repo.RetrieveStatus(new StatusOptions());
        foreach (var untracked in status.Untracked)
        {
            string path = Path.Combine(_projectModel.DirectoryPath, untracked.FilePath);
            if (File.Exists(path)) File.Delete(path);
        }

        _projectModel.Logger?.Information("Repository rolled back to last commit.");
    }

    private void CommitToBranch(string runId)
    {
        using var repo = new Repository(_projectModel.DirectoryPath);

        string branchName = $"testmap/{runId}";
        Branch branch = repo.Branches[branchName] ?? repo.CreateBranch(branchName);

        Commands.Checkout(repo, branch);
        Commands.Stage(repo, "*");

        Signature author = new Signature("TestMap Bot", "testmap@localhost", DateTimeOffset.Now);
        repo.Commit($"Add generated test for run {runId}", author, author);

        _projectModel.Logger?.Information($"Committed changes to branch {branchName}.");
    }

    private void InsertTestIntoFile(string filePath, string test)
    {
        var file = File.ReadAllLines(filePath).ToList();
        int lastBraceIndex = file.FindLastIndex(line => line.Trim() == "}");
        if (lastBraceIndex == -1)
            throw new InvalidOperationException($"No closing brace found in {filePath}");

        file.Insert(lastBraceIndex, test);
        File.WriteAllLines(filePath, file);
    }
    
    public string? ExtractTestMethodName(string testCode)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(testCode);
            var root = tree.GetRoot();

            // Try to find method declaration (handles both standard and local methods)
            var method = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (method != null)
                return method.Identifier.Text;

            // Fallback to local function
            var localMethod = root.DescendantNodes()
                .OfType<LocalFunctionStatementSyntax>()
                .FirstOrDefault();

            return localMethod?.Identifier.Text;
        }
        catch (Exception ex)
        {
            _projectModel.Logger?.Error($"Failed to extract test method name: {ex.Message}");
            return null;
        }
    }
}
