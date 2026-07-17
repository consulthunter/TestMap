namespace TestMap.EndToEndTests.Fixtures.PinnedTargets;

public static class PinnedTargetFixture
{
    public const string FirstCommit = "0123456789abcdef0123456789abcdef01234567";
    public const string SecondCommit = "89abcdef0123456789abcdef0123456789abcdef";

    public static string CreateSmokeInput(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "pinned-targets.csv");
        File.WriteAllText(path,
            "name,lastCommitSHA\n" +
            $"local/example,{FirstCommit}\n" +
            $"local/example,{SecondCommit}\n");
        return path;
    }
}
