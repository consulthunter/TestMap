using TestMap.Models;

namespace TestMap.UnitTests.ProjectModels;

internal static class ProjectModelLogTestData
{
    public static readonly DateTimeOffset RunStartedAtUtc =
        new(2026, 7, 16, 14, 5, 9, TimeSpan.Zero);

    public static ProjectModel Legacy(string logRoot, string owner = "owner", string repository = "repository") =>
        new(
            owner: owner,
            repoName: repository,
            logsDirPath: logRoot,
            runStartedAtUtc: RunStartedAtUtc);
}
