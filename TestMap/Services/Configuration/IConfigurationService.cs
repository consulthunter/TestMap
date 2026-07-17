/*
 * consulthunter
 * 2024-11-07
 * Interface for the configuration
 * service
 * IConfigurationService.cs
 */

using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Targets;

namespace TestMap.Services.Configuration;

public interface IConfigurationService
{
    Task ConfigureRunAsync();
    RunMode RunMode { get; set; }

    TestMapConfig Config { get; }
    DateTimeOffset RunStartedAtUtc { get; }
    string RunDate { get; }
    List<ProjectModel> ProjectModels { get; }
    TargetManifest? TargetManifest { get; }
    string? TargetManifestSha256 { get; }
    string? TargetExecutionReportPath { get; }
}
