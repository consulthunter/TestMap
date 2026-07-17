using TestMap.Models.Targets;
using TestMap.Services.Targets;

namespace TestMap.UnitTests.Targets;

internal static class TargetTestData
{
    public const string Commit = "0123456789abcdef0123456789abcdef01234567";
    public const string OtherCommit = "89abcdef0123456789abcdef0123456789abcdef";
    public const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    public static RepositoryTarget Target(string commit = Commit) =>
        new TargetIdentityService().Create("owner/repository", commit, [1]);

    public static TargetManifest Manifest(params RepositoryTarget[] targets) => new(
        1,
        new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
        new TargetSourceProvenance("targets.csv", Hash),
        new TargetReportReference("targets-rejections.csv", Hash),
        targets.Length == 0 ? [Target()] : targets);
}
