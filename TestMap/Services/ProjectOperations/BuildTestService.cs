using System.Xml.Serialization;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models;
using TestMap.Models.Coverage;

namespace TestMap.Services.ProjectOperations;

public class BuildTestService : IBuildTestService
{
    private readonly ProjectModel _projectModel;

    
    public BuildTestService(ProjectModel project)
    {
        _projectModel = project;
    }

    public async Task BuildTestAsync()
    {
        await CreateDockerContainerAsync();
        LoadTestingResults();
        await DockerCleanupAsync();
    }

    private async Task CreateDockerContainerAsync()
    {
        var scriptRunner = new ScriptRunner();
        
        await scriptRunner.RunPowershellScriptAsync(
            [
                $"{_projectModel.DirectoryPath}", _projectModel.OutputPath, _projectModel.Docker["all"],
                $"{_projectModel.RepoName.ToLower()}-testing"
            ],
            _projectModel.Scripts["Docker"]);
    }
    private async Task DockerCleanupAsync()
    {
        var scriptRunner = new ScriptRunner();
        
        await scriptRunner.RunPowershellScriptAsync(
            [
                $"{_projectModel.RepoName.ToLower()}-testing", _projectModel.LogsFilePath.Replace(".log", ".testing.log.jsonl")
            ],
            _projectModel.Scripts["Docker-Cleanup"]);
    }

    private string FindDockerImage(){
    
        string latest = "";
        string latestImage = "";
            
        // search through each project's framework for a match
        foreach (AnalysisProject analysisProject in _projectModel.Projects)
        {
            // search each available image
            foreach (var image in _projectModel.Docker.Keys)
            {
                if (analysisProject.LanguageFramework.Contains(image))
                {
                    if (latest == "")
                    {
                        latest = image;
                        latestImage = _projectModel.Docker[image];
                    }
                    else if (float.Parse(latest) < float.Parse(image))
                    {
                        latest = image;
                        latestImage = _projectModel.Docker[image];
                    }
                }
            }
        }
        return latestImage;
    }

    private void LoadTestingResults()
    {
        LoadCoverageReport();
    }

    private void LoadCoverageReport()
    {
        XmlSerializer serializer = new XmlSerializer(typeof(CoverageReport));

        try
        {
            using FileStream fileStream =
                new FileStream($"{_projectModel.OutputPath}{Path.DirectorySeparatorChar}merged.cobertura.xml",
                    FileMode.Open);
            _projectModel.CoverageReport = (CoverageReport)serializer.Deserialize(fileStream);
        }
        catch (Exception ex)
        {
            _projectModel.Logger.Error($"Error loading coverage report: {ex.Message}");
            _projectModel.CoverageReport = new CoverageReport();

        }
    }
}