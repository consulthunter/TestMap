/*
 * consulthunter
 * 2024-11-07
 * Core structure for storing information
 * from a repository. Creates directories
 * for log files and output.
 * ProjectModel.cs
 */

using Serilog;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Coverage;
using TestMap.Models.Results;
using TestMap.Models.Targets;
using TestMap.Services.Logging;

namespace TestMap.Models;

public class ProjectModel
{
    private readonly ProjectLogDirectoryAllocator _logDirectoryAllocator;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="gitHubUrl">Full URL for the repository from GitHub</param>
    /// <param name="owner">Name of the account owner for the repo</param>
    /// <param name="repoName">Name of the repo</param>
    /// <param name="runDate">Date (YYYY-MM-DD) of the run</param>
    /// <param name="directoryPath">Absolute path for the repo folder</param>
    /// <param name="logsDirPath">Absolute path for the logs folder</param>
    /// <param name="outputDirPath">Absolute path for the output folder</param>
    /// <param name="tempDirPath">Absolute path for the temp directory.</param>
    /// <param name="testingFrameworks">Testing frameworks defined in the config</param>
    /// <param name="docker">Supported docker images</param>
    /// <param name="scripts">Batch or shell scripts defined in the config</param>
    public ProjectModel(string gitHubUrl = "", string owner = "", string repoName = "", string runDate = "",
        string directoryPath = "", string? logsDirPath = "", string? outputDirPath = "", string? tempDirPath = "",
        string? databasePath = null, TestMapConfig? config = null,
        DateTimeOffset? runStartedAtUtc = null,
        ProjectLogDirectoryAllocator? logDirectoryAllocator = null)
    {
        GitHubUrl = gitHubUrl;
        Owner = owner;
        RepoName = repoName;
        RunStartedAtUtc = ResolveRunStartedAtUtc(runDate, runStartedAtUtc);
        _logDirectoryAllocator = logDirectoryAllocator ?? new ProjectLogDirectoryAllocator();
        Solutions = new List<CSharpSolutionModel>();
        Projects = new List<CSharpProjectModel>();
        DirectoryPath = directoryPath;
        LogsDirPath = logsDirPath;
        OutputDirPath = outputDirPath;
        TempDirPath = tempDirPath;
        DatabasePath = databasePath;
        Config = config ?? new TestMapConfig();

        ContentHash = Utilities.Utilities.ComputeSha256($"{owner}-{repoName}");

        CreateUniqueId();
    }


    // fields
    public string? ProjectId { get; set; }
    public int DbId { get; set; }
    public string GitHubUrl { get; set; }
    public string Owner { get; private set; }
    public string RepoName { get; }
    public TestMapConfig Config { get; set; }

    public string? Branch { get; set; }
    public string? Commit { get; set; }
    public string? LastAnalyzedCommit { get; set; }
    public string? DatabasePath { get; set; }

    public List<CSharpSolutionModel> Solutions { get; set; }
    public List<CSharpProjectModel> Projects { get; set; }
    public CoverageReportModel? CoverageReport { get; set; }
    public List<TestResultModel> TestResults { get; set; } = new();
    public string DirectoryPath { get; set; }
    public string? TempDirPath { get; set; }
    private string? LogsDirPath { get; }
    private string? OutputDirPath { get; }
    public string? OutputPath { get; set; }
    public string? LogsFilePath { get; private set; }
    public DateTimeOffset RunStartedAtUtc { get; }
    public Dictionary<string, List<string>>? TestingFrameworks { get; set; }
    public Dictionary<string, string>? Docker { get; set; }
    public Dictionary<string, string>? Scripts { get; set; }
    public bool IsBaselineEstablished { get; set; }
    public string ContentHash { get; set; }
    public RepositoryTarget? RepositoryTarget { get; set; }
    public MaterializedRevision? MaterializedRevision { get; set; }
    public ILogger? Logger { get; private set; }

    public void BindTarget(RepositoryTarget target)
    {
        RepositoryTarget = target;
        ContentHash = Utilities.Utilities.ComputeSha256($"{Owner}-{RepoName}-{target.TargetId}");
    }

    // methods
    /// <summary>
    ///     Creates a random unique integer to use as the ProjectId
    ///     ProjectID is {randomNumber}_{RepoName}
    /// </summary>
    private void CreateUniqueId()
    {
        var rnd = new Random();
        var randomNumber = rnd.Next(1, 1000001);
        ProjectId = $"{randomNumber}_{Owner}-{RepoName}";
    }

    /// <summary>
    ///     Checks to see if the Log directory
    ///     is present, creates the directory
    ///     if not present
    /// </summary>
    public void EnsureProjectLogDir()
    {
        if (LogsFilePath is not null && Logger is not null) return;

        if (MaterializedRevision is not null)
        {
            var allocation = _logDirectoryAllocator.Allocate(
                LogsDirPath ?? string.Empty,
                RunStartedAtUtc,
                Owner,
                RepoName,
                ProjectId ?? "run",
                "run.log");
            MaterializedRevision = MaterializedRevision with
            {
                Paths = MaterializedRevision.Paths with { LogPath = allocation.LogFilePath }
            };
            CreateLog(allocation.LogFilePath);
            return;
        }
        if (ProjectId != null)
        {
            var allocation = _logDirectoryAllocator.Allocate(
                LogsDirPath ?? string.Empty,
                RunStartedAtUtc,
                Owner,
                RepoName,
                ProjectId);
            CreateLog(allocation.LogFilePath);
        }
    }

    /// <summary>
    ///     Checks to see if the Output directory
    ///     is present, creates the directory
    ///     if not present
    /// </summary>
    public void EnsureProjectOutputDir()
    {
        if (MaterializedRevision is not null)
        {
            Directory.CreateDirectory(MaterializedRevision.Paths.ArtifactPath);
            OutputPath = MaterializedRevision.Paths.ArtifactPath;
            return;
        }
        if (ProjectId != null)
        {
            var outputPath = Path.Combine(OutputDirPath ?? string.Empty, $"{Owner}-{RepoName}");
            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);
            OutputPath = outputPath;
        }
    }

    /// <summary>
    ///     Creates the logger for the project
    /// </summary>
    /// <param name="logFilePath">Absolute file path for the project log</param>
    private void CreateLog(string logFilePath)
    {
        LogsFilePath = logFilePath;

        // logger
        Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(LogsFilePath)
            .CreateLogger();
    }

    private static DateTimeOffset ResolveRunStartedAtUtc(string runDate, DateTimeOffset? supplied)
    {
        if (supplied is { } timestamp)
        {
            if (timestamp.Offset != TimeSpan.Zero)
                throw new ArgumentException("The project run timestamp must be UTC.", nameof(supplied));
            return timestamp;
        }

        var now = DateTimeOffset.UtcNow;
        if (DateOnly.TryParseExact(
                runDate,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var date))
            return new DateTimeOffset(
                date.Year, date.Month, date.Day,
                now.Hour, now.Minute, now.Second, now.Millisecond,
                TimeSpan.Zero);
        return now;
    }
}
