using TestMap.Models.Targets;
using TestMap.Services.Targets;

namespace TestMap.UnitTests.Targets;

public sealed class SingleRepositoryUrlParserTests
{
    private readonly SingleRepositoryUrlParser _parser = new(
        new TargetIdentityService(), new TargetFingerprintService());

    [Theory]
    [InlineData("https://github.com/Owner/Repository", "owner/repository")]
    [InlineData("https://github.com/owner/repository.git", "owner/repository")]
    [InlineData("https://GITHUB.COM/owner/repository/", "owner/repository")]
    public void Parse_ValidUrl_NormalizesRepository(string url, string expected)
    {
        var request = _parser.Parse(url, RepositoryAuthenticationMode.Anonymous);
        Assert.Equal(expected, request.Repository);
        Assert.Equal($"https://github.com/{expected}.git", request.CanonicalUrl);
        Assert.Equal(url, request.RequestedUrl);
    }

    [Theory]
    [InlineData("http://github.com/owner/repo")]
    [InlineData("https://example.com/owner/repo")]
    [InlineData("https://token@github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo?x=1")]
    [InlineData("https://github.com/owner/repo#readme")]
    [InlineData("https://github.com/owner/repo/issues/1")]
    [InlineData("https://github.com/owner/%2e%2e")]
    [InlineData(" https://github.com/owner/repo")]
    public void Parse_InvalidUrl_Throws(string url) =>
        Assert.Throws<InvalidDataException>(() =>
            _parser.Parse(url, RepositoryAuthenticationMode.Anonymous));
}
