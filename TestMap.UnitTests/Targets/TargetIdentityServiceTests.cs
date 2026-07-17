using TestMap.Services.Targets;

namespace TestMap.UnitTests.Targets;

public sealed class TargetIdentityServiceTests
{
    private readonly TargetIdentityService _service = new();

    [Theory]
    [InlineData("Owner/Repo", "owner/repo")]
    [InlineData("https://github.com/Owner/Repo.git", "owner/repo")]
    public void TryNormalizeRepository_ValidName_ReturnsCanonicalIdentity(string input, string expected)
    {
        Assert.True(_service.TryNormalizeRepository(input, out var actual));
        Assert.Equal(expected, actual);
        Assert.Equal($"https://github.com/{expected}.git", TargetIdentityService.CanonicalGitHubUrl(actual));
    }

    [Theory]
    [InlineData("repo")]
    [InlineData("owner/repo/extra")]
    [InlineData("owner/../repo")]
    public void TryNormalizeRepository_InvalidName_ReturnsFalse(string input) =>
        Assert.False(_service.TryNormalizeRepository(input, out _));

    [Fact]
    public void TryNormalizeCommit_RequiresFullSha()
    {
        Assert.True(_service.TryNormalizeCommit(TargetTestData.Commit.ToUpperInvariant(), out var commit));
        Assert.Equal(TargetTestData.Commit, commit);
        Assert.False(_service.TryNormalizeCommit(TargetTestData.Commit[..12], out _));
        Assert.False(_service.TryNormalizeCommit(new string('0', 40), out _));
    }

    [Fact]
    public void CreateTargetId_IsStableAndRevisionSpecific()
    {
        var first = TargetIdentityService.CreateTargetId("owner/repository", TargetTestData.Commit);
        Assert.Equal(64, first.Length);
        Assert.Equal(first, TargetIdentityService.CreateTargetId("OWNER/REPOSITORY", TargetTestData.Commit.ToUpperInvariant()));
        Assert.NotEqual(first, TargetIdentityService.CreateTargetId("owner/repository", TargetTestData.OtherCommit));
    }
}
