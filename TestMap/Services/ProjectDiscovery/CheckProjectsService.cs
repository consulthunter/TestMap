using Octokit;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.App;

namespace TestMap.Services.ProjectDiscovery;

public class CheckProjectsService : ICheckProjectsService
{
    private readonly GitHubClient _client;
    private readonly TestMapConfig _config;
    private readonly ProjectContext _context;

    // Serialize writes within this process (across all concurrent projects)
    private static readonly SemaphoreSlim _fileWriteGate = new(1, 1);

    public CheckProjectsService(TestMapConfig config, string token, ProjectContext context)
    {
        _config = config;
        _client = new GitHubClient(new ProductHeaderValue("TestMap"));
        _client.Credentials = new Credentials(token);
        _context = context;
    }

    public async Task ProcessRepositoryAsync()
    {
        if (!File.Exists(_config.RuntimeConfig.FilePaths.TargetFilePath))
            throw new FileNotFoundException("Repo list file not found", _config.RuntimeConfig.FilePaths.TargetFilePath);

        var baseDir = Path.GetDirectoryName(_config.RuntimeConfig.FilePaths.TargetFilePath) ??
                      throw new InvalidOperationException();

        _context.Project.Logger?.Information("Checking {Owner}/{RepoName} ...", _context.Project.Owner,
            _context.Project.RepoName);

        var hasTests = await RepoLikelyHasTests(_context.Project.Owner, _context.Project.RepoName);

        // Output files
        var withFile = Path.Combine(baseDir, "repos_with_tests.txt");
        var withoutFile = Path.Combine(baseDir, "repos_without_tests.txt");

        await _fileWriteGate.WaitAsync();
        try
        {
            if (hasTests)
                await File.AppendAllLinesAsync(withFile, new[] { _context.Project.GitHubUrl });
            else
                await File.AppendAllLinesAsync(withoutFile, new[] { _context.Project.GitHubUrl });
        }
        finally
        {
            _fileWriteGate.Release();
        }

        _context.Project.Logger?.Information("Done.");
    }

    private async Task<bool> RepoLikelyHasTests(string owner, string repo)
    {
        // 1. Cheap check: does the top-level listing contain a test indicator?
        IReadOnlyList<RepositoryContent> topLevel;
        try
        {
            topLevel = await _client.Repository.Content.GetAllContents(owner, repo);
        }
        catch (NotFoundException)
        {
            // Empty repo or no default-branch content. Not an error — fall through to the
            // recursive tree scan below, which resolves the definitive answer.
            _context.Project.Logger?.Debug(
                "Top-level contents not found for {Owner}/{Repo}; falling back to tree scan.", owner, repo);
            topLevel = Array.Empty<RepositoryContent>();
        }
        catch (Exception ex)
        {
            // Auth, rate limit, or any other API failure here means we cannot determine test
            // presence — fail fast instead of silently reporting "no tests".
            throw DescribeGitHubFailure(owner, repo, ex);
        }

        if (ContainsTestIndicators(topLevel.Select(c => c.Name)))
            return true;

        // 2. Thorough check: scan the recursive tree for any test-related path.
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
            // Repo, default branch, or tree is genuinely inaccessible (missing repo, empty repo
            // with no commits, or a private repo the token cannot see). Definitive negative, but
            // logged so a mis-scoped token is not silently mistaken for "no tests".
            _context.Project.Logger?.Warning(
                "GitHub returned not-found while scanning {Owner}/{Repo}; treating as no tests. " +
                "If this repository is private, ensure GITHUB_TOKEN has access to it.", owner, repo);
            return false;
        }
        catch (Exception ex)
        {
            throw DescribeGitHubFailure(owner, repo, ex);
        }
    }

    /// <summary>
    /// Logs an explicit, human-readable reason for a GitHub API failure and returns an exception
    /// to throw so the run fails fast rather than misclassifying the repository as having no tests.
    /// </summary>
    private InvalidOperationException DescribeGitHubFailure(string owner, string repo, Exception ex)
    {
        var reason = ex switch
        {
            RateLimitExceededException rate =>
                $"GitHub API rate limit exceeded (resets at {rate.Reset.UtcDateTime:u} UTC). " +
                "Provide a GITHUB_TOKEN with sufficient quota, or wait for the reset.",
            AuthorizationException =>
                "GitHub rejected the credentials (HTTP 401). GITHUB_TOKEN is missing, invalid, " +
                "expired, or lacks the required scope (classic: repo or public_repo; " +
                "fine-grained: Contents read). Set a valid token in TestMap/.env and retry.",
            ApiException api =>
                $"GitHub API request failed (HTTP {(int)api.StatusCode}): {api.Message}",
            _ =>
                $"Unexpected error querying the GitHub API: {ex.Message}"
        };

        var message =
            $"check-projects could not determine test presence for {owner}/{repo}. {reason}";
        _context.Project.Logger?.Error(ex, "{Message}", message);
        return new InvalidOperationException(message, ex);
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