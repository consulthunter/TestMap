namespace TestMap.UnitTests.Services.Logging;

internal static class ProjectLogDirectoryTestData
{
    public static readonly DateTimeOffset RunStartedAtUtc =
        new(2026, 7, 16, 14, 5, 9, TimeSpan.Zero);

    public const string Owner = "PowerShell";
    public const string Repository = "PlatyPS";
    public const string ProjectId = "482731_PowerShell-PlatyPS";

    public static string CreateRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "testmap-readable-logs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
