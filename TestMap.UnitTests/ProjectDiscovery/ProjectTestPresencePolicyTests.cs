using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery;

namespace TestMap.UnitTests.ProjectDiscovery;

public sealed class ProjectTestPresencePolicyTests
{
    private readonly ProjectTestPresencePolicy _policy = new();

    [Theory]
    [InlineData("tests/Widget.cs", ProjectCheckEvidenceCategory.TestDirectory)]
    [InlineData("src/Widget.Tests.csproj", ProjectCheckEvidenceCategory.TestProjectFile)]
    [InlineData("src/ParserTests.cs", ProjectCheckEvidenceCategory.TestSourceFile)]
    [InlineData("src/parser_tests.cs", ProjectCheckEvidenceCategory.TestSourceFile)]
    public void FindEvidence_RecognizesBoundedIndicators(string path, ProjectCheckEvidenceCategory category)
    {
        var evidence = _policy.FindEvidence([path]);
        Assert.NotNull(evidence);
        Assert.Equal(category, evidence.Category);
    }

    [Theory]
    [InlineData("src/Contest.cs")]
    [InlineData("docs/testing-guide.md")]
    [InlineData("contest/Widget.cs")]
    [InlineData("src/latest.cs")]
    public void FindEvidence_DoesNotUseSubstringMatches(string path) =>
        Assert.Null(_policy.FindEvidence([path]));

    [Fact]
    public void FindEvidence_IsDeterministicAcrossProviderOrdering()
    {
        var first = _policy.FindEvidence(["z/ThingTests.cs", "a/Other.Tests.csproj"]);
        var second = _policy.FindEvidence(["a/Other.Tests.csproj", "z/ThingTests.cs"]);
        Assert.Equal(first, second);
        Assert.Equal("a/Other.Tests.csproj", first?.Path);
    }
}
