/*
 * consulthunter
 * 2024-11-07
 * Interface for the configuration
 * service
 * IConfigurationService.cs
 */

using TestMap.Models;

namespace TestMap.Services.Configuration;

public interface IConfigurationService
{
    Task ConfigureRunAsync();
    int GetConcurrency();
    List<ProjectModel> GetProjectModels();
    string GetRunDate();
    string? GetLogsDirectory();
    string? GetTempDirPath();
    Dictionary<string, string>? GetScripts();
    Dictionary<string, string>? GetEnvironmentVariables();
}