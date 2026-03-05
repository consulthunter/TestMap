using Octokit;
using TestMap.Models;
using TestMap.Models.Configuration;

namespace TestMap.Services.ProjectOperations;

public class CheckProjectsService : ICheckProjectsService
{
    private readonly GitHubClient _client;
    private readonly TestMapConfig _config;
    private readonly ProjectModel _projectModel;

    // Serialize writes within this process (across all concurrent projects)
    private static readonly SemaphoreSlim _fileWriteGate = new(1, 1);

    public CheckProjectsService(TestMapConfig config, string token, ProjectModel projectModel)
    {
        _config = config;
        _client = new GitHubClient(new ProductHeaderValue("TestMap"));
        _client.Credentials = new Credentials(token);
        _projectModel = projectModel;
    }

    public async Task ProcessRepositoryAsync()
    {
        if (!File.Exists(_config.FilePaths.TargetFilePath))
            throw new FileNotFoundException("Repo list file not found", _config.FilePaths.TargetFilePath);

        var baseDir = Path.GetDirectoryName(_config.FilePaths.TargetFilePath) ?? throw new InvalidOperationException();

        Console.WriteLine($"Checking {_projectModel.Owner}/{_projectModel.RepoName} ...");

        var hasTests = await RepoLikelyHasTests(_projectModel.Owner, _projectModel.RepoName);

        // Output files
        var withFile = Path.Combine(baseDir, "repos_with_tests.txt");
        var withoutFile = Path.Combine(baseDir, "repos_without_tests.txt");

        await _fileWriteGate.WaitAsync();
        try
        {
            if (hasTests)
                await File.AppendAllLinesAsync(withFile, new[] { _projectModel.GitHubUrl });
            else
                await File.AppendAllLinesAsync(withoutFile, new[] { _projectModel.GitHubUrl });
        }
        finally
        {
            _fileWriteGate.Release();
        }

        Console.WriteLine("Done.");
    }

    private async Task<bool> RepoLikelyHasTests(string owner, string repo)
    {
        // 1. First try to check top-level structure
        IReadOnlyList<RepositoryContent> topLevel;
        try
        {
            topLevel = await _client.Repository.Content.GetAllContents(owner, repo);
        }
        catch
        {
            return false;
        }

        if (ContainsTestIndicators(topLevel.Select(c => c.Name)))
            return true;

        // 2. Check repo tree recursively (lightweight)
        try
        {
            var repoInfo = await _client.Repository.Get(owner, repo);
            var defaultBranch = repoInfo.DefaultBranch;

            var reference = await _client.Git.Reference.Get(owner, repo, $"heads/{defaultBranch}");
            var tree = await _client.Git.Tree.GetRecursive(owner, repo, reference.Object.Sha);

            return tree.Tree.Any(t => t.Path.Contains("test", StringComparison.OrdinalIgnoreCase));
        }
        catch (NotFoundException)
        {
            return false;
        }
        catch (ApiException apiEx)
        {
            Console.WriteLine(apiEx.Message);
            return false;
        }
    }

    private bool ContainsTestIndicators(IEnumerable<string> names)
    {
        var lower = names.Select(n => n.ToLower()).ToList();

        string[] indicators =
        {
            "test"
        };

        return lower.Any(n => indicators.Any(i => n.Contains(i)));
    }
}