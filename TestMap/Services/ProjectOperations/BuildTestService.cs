using System.Xml.Serialization;
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

        if (_projectModel.OutputPath != null)
            if (_projectModel.Docker != null)
                if (_projectModel.Scripts != null)
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

        if (_projectModel.LogsFilePath != null)
            if (_projectModel.Scripts != null)
                await scriptRunner.RunPowershellScriptAsync(
                    [
                        $"{_projectModel.RepoName.ToLower()}-testing",
                        _projectModel.LogsFilePath.Replace(".log", ".testing.log.jsonl")
                    ],
                    _projectModel.Scripts["Docker-Cleanup"]);
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
            _projectModel.CoverageReport = serializer.Deserialize(fileStream) as CoverageReport;
        }
        catch (Exception ex)
        {
            _projectModel.Logger?.Error($"Error loading coverage report: {ex.Message}");
            _projectModel.CoverageReport = new CoverageReport();

        }
    }
}