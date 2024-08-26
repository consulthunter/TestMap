
using Newtonsoft.Json.Linq;
using TestMap.Models;

namespace TestMap.Services;

public class ConfigurationService
{
    // fields
    private readonly string _targetFilePath;
    public readonly string LogsDirPath;
    private readonly string _tempDirPath;
    private readonly string _outputDirPath;
    public readonly int MaxConcurrency;
    public string RunDate;
    public List<ProjectModel> ProjectModels = new();
    // methods
    public ConfigurationService(JObject settings)
    {
        // Read settings from configuration file
        _targetFilePath = settings["FilePaths"]["TargetFilePath"].ToString();
        LogsDirPath = settings["FilePaths"]["LogsDirPath"].ToString();
        _tempDirPath = settings["FilePaths"]["TempDirPath"].ToString();
        _outputDirPath = settings["FilePaths"]["OutputDirPath"].ToString();
        MaxConcurrency = int.Parse(settings["Settings"]["MaxConcurrency"].ToString());
        RunDate = DateTime.UtcNow.ToString(settings["Settings"]["RunDateFormat"].ToString());
    }

    public async Task ConfigureRunAsync()
    {
        EnsureRunLogsDirectory();
        EnsureTempDirectory();
        EnsureRunOutputDirectory();
        await ReadTarget();
    }
    private async Task ReadTarget()
    {
        if (Path.Exists(_targetFilePath))
        {
            // Open the file for reading using StreamReader
            using (StreamReader sr = new StreamReader(_targetFilePath))
            {
                string? line;

                // Read and display lines from the file until the end of the file is reached
                while ((line = await sr.ReadLineAsync()) != null)
                {
                    InitializeProjectModel(line);
                }
            }
        }
    }
    private void InitializeProjectModel(string projectUrl)
    {
        string githubUrl = projectUrl;
        (string owner, string repoName) = ExtractOwnerAndRepo(githubUrl);
        string directoryPath = Path.Combine(_tempDirPath, repoName);
        string logDirPath = Path.Combine(LogsDirPath, RunDate);
        string outputPath = Path.Combine(_outputDirPath, RunDate);

        ProjectModel projectModel = new ProjectModel(gitHubUrl: githubUrl, owner: owner, repoName: repoName,
            directoryPath: directoryPath, tempDirPath: _tempDirPath);
        
        EnsureProjectLogDir(projectModel);
        EnsureProjectOutputDir(projectModel);

        ProjectModels.Add(projectModel);
    }
    
    private void EnsureRunLogsDirectory()
    {
        // Check if Logs directory exists, create if not
        if (!Directory.Exists(LogsDirPath))
        {
            Directory.CreateDirectory(LogsDirPath);

            if (!Directory.Exists(Path.Combine(LogsDirPath, RunDate)))
            {
                Directory.CreateDirectory(Path.Combine(LogsDirPath, RunDate));
            }
        }
    }
    
    private void EnsureTempDirectory()
    {
        // Check if Temp directory exists, create if not
        if (!Directory.Exists(_tempDirPath))
        {
            Directory.CreateDirectory(_tempDirPath);
            
        }
    }
    
    private void EnsureRunOutputDirectory()
    {
        // Check if Output directory exists, create if not
        if (!Directory.Exists(_outputDirPath))
        {
            Directory.CreateDirectory(_outputDirPath);
            
            if (!Directory.Exists(Path.Combine(_outputDirPath, RunDate)))
            {
                Directory.CreateDirectory(Path.Combine(_outputDirPath, RunDate));
            }
        }
    }
    public (string, string) ExtractOwnerAndRepo(string url)
    {

        if (url.StartsWith("https://"))
        {
            // HTTP(S) URL format: https://github.com/owner/repoName
            return ExtractOwnerAndRepoFromHttpUrl(url);
        }
        else
        {
            throw new ArgumentException("Unsupported URL format");
        }
    }
    private (string, string) ExtractOwnerAndRepoFromHttpUrl(string url)
    {
        Uri uri = new Uri(url);
        string owner = uri.Segments[1].TrimEnd('/');
        string repoName = uri.Segments[2].TrimEnd('/');

        return (owner, repoName);
    }

    private void EnsureProjectLogDir(ProjectModel project)
    {
        string logDirPath = Path.Combine(LogsDirPath, RunDate, project.ProjectId);
        
        if (!Directory.Exists(logDirPath))
        {
            Directory.CreateDirectory(logDirPath);
            project.SetLogFilePath(logDirPath);
        }
    }

    private void EnsureProjectOutputDir(ProjectModel project)
    {
        string outputPath = Path.Combine(_outputDirPath, RunDate, project.ProjectId);
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
            project.OutputPath = outputPath;
        }
    }
}