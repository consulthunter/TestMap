using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LibGit2Sharp;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Database;
using TestMap.Models.Results;
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

    private void CommitToBranch(string runId)
    {
        using var repo = new Repository(_projectModel.DirectoryPath);

        var branchName = $"testmap/{runId}";
        var branch = repo.Branches[branchName] ?? repo.CreateBranch(branchName);

        Commands.Checkout(repo, branch);
        Commands.Stage(repo, "*");

        var author = new Signature("TestMap Bot", "testmap@localhost", DateTimeOffset.Now);
        repo.Commit($"Add generated test for run {runId}", author, author);

        _projectModel.Logger?.Information($"Committed changes to branch {branchName}.");
    }
}