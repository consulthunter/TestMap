using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TestMap.Models;

public class ProjectModel
{
    // fields
    public string ProjectId { get; set; }
    public string GitHubUrl { get; set; }
    public string Owner { get; private set; }
    public string RepoName { get; private set; }
    public List<AnalysisSolution> Solutions { get; set; }
    public List<AnalysisProject> Projects { get; set; }
    public string DirectoryPath { get; set; }
    public string TempDirPath {get; set;}
    public string OutputPath { get; set; }
    public string LogsFilePath { get; private set; }
    // methods
    private void CreateUniqueId()
    {
        Random rnd = new Random();
        int randomNumber = rnd.Next(1, 101);
        ProjectId = $"{randomNumber}_{RepoName}";
    }

    public void SetLogFilePath(string logDirPath)
    {
        string logFilePath = Path.Combine(logDirPath, $"{ProjectId}.log");
        LogsFilePath = logFilePath;
    }
    
    // constructors
    public ProjectModel(string gitHubUrl = "", string owner = "", string repoName = "",
        string directoryPath = "", string tempDirPath = "")
    {
        GitHubUrl = gitHubUrl;
        Owner = owner;
        RepoName = repoName;
        Solutions = new ();
        Projects = new List<AnalysisProject>();
        DirectoryPath = directoryPath;
        TempDirPath = tempDirPath;
        
        CreateUniqueId();
    }
}