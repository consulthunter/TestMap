using TestMap.Services.Targets;

namespace TestMap.UnitTests.Targets;

public sealed class SingleRepositoryResolutionSanitizerTests
{
    [Theory]
    [InlineData("Authorization: Bearer secret-value")]
    [InlineData("bearer secret-value")]
    [InlineData("token=secret-value")]
    [InlineData("password: secret-value")]
    [InlineData("api_key=secret-value")]
    [InlineData("ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")]
    [InlineData("github_pat_ABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789")]
    public void Sanitize_RemovesCredentialShapedValues(string value)
    {
        var result = new SingleRepositoryResolutionSanitizer().Sanitize(value);

        Assert.Contains("[redacted]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", result, StringComparison.Ordinal);
        Assert.DoesNotContain("ABCDEFGHIJKLMNOPQRSTUVWXYZ", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_RemovesLineBreaksAndBoundsLength()
    {
        var result = new SingleRepositoryResolutionSanitizer().Sanitize(
            "first\r\nsecond " + new string('x', 500));

        Assert.DoesNotContain('\r', result);
        Assert.DoesNotContain('\n', result);
        Assert.Equal(300, result.Length);
    }
}
