using TestMap.Services.Experiment.Reporting;

namespace TestMap.UnitTests.Experiment;

public sealed class AttemptKeyFactoryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_IsStableAndSeparatesRunsLanesAndAttempts()
    {
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        const string otherCommit = "89abcdef0123456789abcdef0123456789abcdef";
        var key = AttemptKeyFactory.Create("owner/repo", commit, "series", "run-a", "agent", "work-1", 1);

        Assert.Equal(key, AttemptKeyFactory.Create("owner/repo", commit, "series", "run-a", "agent", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("other/repo", commit, "series", "run-a", "agent", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("owner/repo", otherCommit, "series", "run-a", "agent", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("owner/repo", commit, "other-series", "run-a", "agent", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("owner/repo", commit, "series", "run-b", "agent", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("owner/repo", commit, "series", "run-a", "testmap", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("owner/repo", commit, "series", "run-a", "agent", "work-1", 2));
        Assert.Equal(64, key.Length);
    }
}
