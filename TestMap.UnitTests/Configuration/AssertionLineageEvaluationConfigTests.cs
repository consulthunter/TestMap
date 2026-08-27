using TestMap.Models.Configuration.Experiment;
using TestMap.Services.StaticAnalysis.Assertions;

namespace TestMap.UnitTests.Configuration;

public sealed class AssertionLineageEvaluationConfigTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Defaults_AreEnabledAtFourHops()
    {
        var config = new AssertionLineageEvaluationConfig();
        var policy = AssertionLineagePolicy.Resolve(config);

        Assert.True(config.Enabled);
        Assert.Equal(4, config.MaxDepth);
        Assert.True(policy.Enabled);
        Assert.Equal(4, policy.MaxDepth);
        Assert.Equal("assertion-lineage-v1", policy.PolicyVersion);
        Assert.Equal("assertion-catalog-v1", policy.CatalogVersion);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65)]
    public void Resolve_InvalidDepth_Throws(int depth)
    {
        var config = new AssertionLineageEvaluationConfig { MaxDepth = depth };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssertionLineagePolicy.Resolve(config));
        Assert.False(AssertionLineagePolicy.TryValidate(config, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
