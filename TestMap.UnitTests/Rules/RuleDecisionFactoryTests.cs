using TestMap.Models.Rules;
using TestMap.Rules;

namespace TestMap.UnitTests.Rules;

public sealed class RuleDecisionFactoryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void CreateDecision_MapsRuleDefinitionAndEvidence()
    {
        var rule = new RuleDefinition
        {
            Id = "test.rule",
            Version = "1.2",
            Name = "Test rule",
            Description = "Rule used by unit tests.",
            Category = "Unit"
        };
        var evidence = RuleDecisionFactory.CreateEvidence(
            "Source",
            "Key",
            "Value",
            "Location");

        var decision = RuleDecisionFactory.CreateDecision(
            "DecisionKind",
            "DecisionValue",
            rule,
            RuleConfidence.High,
            [evidence],
            "Decision notes.");

        Assert.Equal("DecisionKind", decision.DecisionKind);
        Assert.Equal("DecisionValue", decision.Value);
        Assert.Equal(rule.Id, decision.RuleId);
        Assert.Equal(rule.Version, decision.RuleVersion);
        Assert.Equal(RuleConfidence.High, decision.Confidence);
        Assert.Equal("Decision notes.", decision.Notes);
        var actualEvidence = Assert.Single(decision.Evidence);
        Assert.Equal("Source", actualEvidence.Source);
        Assert.Equal("Key", actualEvidence.Key);
        Assert.Equal("Value", actualEvidence.Value);
        Assert.Equal("Location", actualEvidence.Location);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CreateDecision_UsesEmptyEvidenceWhenNull()
    {
        var rule = new RuleDefinition
        {
            Id = "test.rule",
            Version = "1.0"
        };

        var decision = RuleDecisionFactory.CreateDecision(
            "DecisionKind",
            "DecisionValue",
            rule,
            RuleConfidence.Low);

        Assert.Empty(decision.Evidence);
        Assert.Equal(string.Empty, decision.Notes);
    }
}
