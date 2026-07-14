using TestMap.Services.Experiment.Reporting;

namespace TestMap.UnitTests.Experiment;

public sealed class AttemptKeyFactoryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_IsStableAndSeparatesRunsLanesAndAttempts()
    {
        var key = AttemptKeyFactory.Create("owner/repo", "series", "run-a", "agent", "work-1", 1);

        Assert.Equal(key, AttemptKeyFactory.Create("owner/repo", "series", "run-a", "agent", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("other/repo", "series", "run-a", "agent", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("owner/repo", "other-series", "run-a", "agent", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("owner/repo", "series", "run-b", "agent", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("owner/repo", "series", "run-a", "testmap", "work-1", 1));
        Assert.NotEqual(key, AttemptKeyFactory.Create("owner/repo", "series", "run-a", "agent", "work-1", 2));
        Assert.Equal(64, key.Length);
    }
}
