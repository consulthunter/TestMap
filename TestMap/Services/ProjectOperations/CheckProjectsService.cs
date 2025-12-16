using Octokit;
using TestMap.Models.Configuration;

namespace TestMap.Services.ProjectOperations;

public class CheckProjectsService : ICheckProjectsService
{
    private readonly GitHubClient _client;
    private readonly TestMapConfig _config;

    public CheckProjectsService(TestMapConfig config, string token)
    {
        _config = config;
        _client = new GitHubClient(new ProductHeaderValue("TestMap"));
        _client.Credentials = new Credentials(token);
    }

    public async Task ProcessRepositoryListAsync()
    {
        if (!File.Exists(_config.FilePaths.TargetFilePath))
            throw new FileNotFoundException("Repo list file not found", _config.FilePaths.TargetFilePath);

        var baseDir = Path.GetDirectoryName(_config.FilePaths.TargetFilePath)!;

        var fileRepos = await File.ReadAllLinesAsync(_config.FilePaths.TargetFilePath);

        var repos = fileRepos.Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var withTests = new List<string>();
        var withoutTests = new List<string>();

        foreach (var repoUrl in repos)
            try
            {
                var (owner, name) = Utilities.Utilities.ExtractOwnerAndRepo(repoUrl);

                Console.WriteLine($"Checking {owner}/{name} ...");

                var hasTests = await RepoLikelyHasTests(owner, name);

                if (hasTests)
                    withTests.Add(repoUrl);
                else
                    withoutTests.Add(repoUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {repoUrl}: {ex.Message}");
                withoutTests.Add($"{repoUrl}    # ERROR");
            }

        // Output files
        var withFile = Path.Combine(baseDir, "repos_with_tests.txt");
        var withoutFile = Path.Combine(baseDir, "repos_without_tests.txt");

        await File.WriteAllLinesAsync(withFile, withTests);
        await File.WriteAllLinesAsync(withoutFile, withoutTests);

        Console.WriteLine("Done.");
        Console.WriteLine($"Repos with tests: {withTests.Count}");
        Console.WriteLine($"Repos without tests: {withoutTests.Count}");
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
        catch (NotFoundException nf)
        {
            // branch or repo not found
            return false;
        }
        catch (ApiException apiEx)
        {
            // GitHub API error
            Console.WriteLine(apiEx.Message);
            return false;
        }

        return false;
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