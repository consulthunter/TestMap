/*
 * consulthunter
 * 2024-11-07
 * Interface for the configuration
 * service
 * IConfigurationService.cs
 */

using TestMap.Models;
using TestMap.Models.Configuration;

namespace TestMap.Services.Configuration;

public interface IConfigurationService
{
    Task ConfigureRunAsync();
    RunMode RunMode { get; set; }

    TestMapConfig Config { get; }
    string RunDate { get; }
    List<ProjectModel> ProjectModels { get; }
}