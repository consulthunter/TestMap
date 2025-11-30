/*
 * consulthunter
 * 2024-11-07
 * Core structure for storing information
 * from a repository. Creates directories
 * for log files and output.
 * ProjectModel.cs
 */

using Serilog;
using TestMap.Models.Configuration;
using TestMap.Models.Coverage;
using TestMap.Models.Results;

namespace TestMap.Models;

public class ProjectModel
{
    private readonly string _runDate;

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
        Dictionary<string, List<string>>? testingFrameworks = null, Dictionary<string, string>? docker = null,
        string? databasePath = null, TestMapConfig config = null)
    {
        GitHubUrl = gitHubUrl;
        Owner = owner;
        RepoName = repoName;
        _runDate = runDate;
        Solutions = new List<AnalysisSolution>();
        Projects = new List<AnalysisProject>();
        DirectoryPath = directoryPath;
        LogsDirPath = logsDirPath;
        OutputDirPath = outputDirPath;
        TempDirPath = tempDirPath;
        TestingFrameworks = testingFrameworks;
        Docker = docker;
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

    public List<AnalysisSolution> Solutions { get; set; }
    public List<AnalysisProject> Projects { get; set; }
    public CoverageReport? CoverageReport { get; set; }
    public List<TrxTestResult> TestResults { get; set; } = new();
    public string DirectoryPath { get; set; }
    public string? TempDirPath { get; set; }
    private string? LogsDirPath { get; }
    private string? OutputDirPath { get; }
    public string? OutputPath { get; set; }
    public string? LogsFilePath { get; private set; }
    public Dictionary<string, List<string>>? TestingFrameworks { get; set; }
    public Dictionary<string, string>? Docker { get; set; }
    public Dictionary<string, string>? Scripts { get; set; }
    public bool IsBaselineEstablished { get; set; }
    public string ContentHash { get; set; }
    public ILogger? Logger { get; private set; }

    // methods
    /// <summary>
    ///     Creates a random unique integer to use as the ProjectId
    ///     ProjectID is {randomNumber}_{RepoName}
    /// </summary>
    private void CreateUniqueId()
    {
        var rnd = new Random();
        var randomNumber = rnd.Next(1, 1000001);
        ProjectId = $"{randomNumber}_{RepoName}";
    }

    /// <summary>
    ///     Checks to see if the Log directory
    ///     is present, creates the directory
    ///     if not present
    /// </summary>
    public void EnsureProjectLogDir()
    {
        if (ProjectId != null)
        {
            var logDirPath = Path.Combine(LogsDirPath ?? string.Empty, _runDate, ProjectId);

            if (!Directory.Exists(logDirPath)) Directory.CreateDirectory(logDirPath);
            CreateLog(logDirPath);
        }
    }

    /// <summary>
    ///     Checks to see if the Output directory
    ///     is present, creates the directory
    ///     if not present
    /// </summary>
    public void EnsureProjectOutputDir()
    {
        if (ProjectId != null)
        {
            var outputPath = Path.Combine(OutputDirPath ?? string.Empty, RepoName);
            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);
            OutputPath = outputPath;
        }
    }

    /// <summary>
    ///     Creates the logger for the project
    /// </summary>
    /// <param name="logDirPath">Absolute file path for the project's log directory</param>
    private void CreateLog(string logDirPath)
    {
        var logFilePath = Path.Combine(logDirPath, $"{ProjectId}.log");
        LogsFilePath = logFilePath;

        // logger
        Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(LogsFilePath)
            .CreateLogger();
    }
}