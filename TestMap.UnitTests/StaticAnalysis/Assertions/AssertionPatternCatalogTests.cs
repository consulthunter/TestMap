using Microsoft.CodeAnalysis.Operations;
using TestMap.Models.Experiment.Assertions;
using TestMap.Services.StaticAnalysis.Assertions;

namespace TestMap.UnitTests.StaticAnalysis.Assertions;

public sealed class AssertionPatternCatalogTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Xunit.Assert.Equal(1, new Product.Service().GetValue());", "Equal", "xUnit")]
    [InlineData("NUnit.Framework.Assert.That(new Product.Service().GetValue(), NUnit.Framework.Is.EqualTo(1));", "That", "NUnit")]
    [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(1, new Product.Service().GetValue());", "AreEqual", "MSTest")]
    [InlineData("new Product.Service().GetValue().Should().Be(1);", "Be", "FluentAssertions")]
    [InlineData("new Product.Service().GetValue().ShouldBe(1);", "ShouldBe", "Shouldly")]
    public async Task Match_SupportedTerminal_IsSemanticAndSupported(
        string body,
        string terminal,
        string framework)
    {
        var fixture = AssertionLineageTestWorkspace.Create(body);
        var (model, _) = await fixture.GetTestMethodAsync();
        var syntax = await fixture.GetInvocationAsync(terminal);
        var operation = Assert.IsAssignableFrom<IInvocationOperation>(model.GetOperation(syntax));

        var match = AssertionPatternCatalog.Shared.Match(operation);

        Assert.NotNull(match);
        Assert.True(match.IsSupported);
        Assert.Equal(framework, match.Framework);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Match_FluentShouldAnchor_IsNotLogicalAssertion()
    {
        var fixture = AssertionLineageTestWorkspace.Create(
            "new Product.Service().GetValue().Should().Be(1);");
        var (model, _) = await fixture.GetTestMethodAsync();
        var syntax = await fixture.GetInvocationAsync("Should");
        var operation = Assert.IsAssignableFrom<IInvocationOperation>(model.GetOperation(syntax));

        Assert.Null(AssertionPatternCatalog.Shared.Match(operation));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Contains", "actual.Contains(\"a\")")]
    [InlineData("Single", "items.Single()")]
    [InlineData("All", "items.All(x => x > 0)")]
    [InlineData("Empty", "items.Empty()")]
    public void MatchLegacy_OperandCallWithoutAssertionReceiver_IsNotRecognized(
        string methodName,
        string invocationText)
    {
        Assert.Null(AssertionPatternCatalog.Shared.MatchLegacy(
            methodName,
            invocationText: invocationText));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("IsTrue", "Assert.IsTrue(actual.Contains(\"a\"))")]
    [InlineData("AreEqual", "Assert.AreEqual(1, actual)")]
    [InlineData("Contains", "StringAssert.Contains(actual, \"a\")")]
    [InlineData("ShouldBe", "actual.ShouldBe(1)")]
    public void MatchLegacy_AssertionCallSiteWithoutSymbol_IsRecognizedAsFallback(
        string methodName,
        string invocationText)
    {
        var match = AssertionPatternCatalog.Shared.MatchLegacy(
            methodName,
            invocationText: invocationText);

        Assert.NotNull(match);
        Assert.Equal("LegacyOrCustom", match.Framework);
        Assert.Equal(AssertionRecognitionKind.SyntacticFallback, match.RecognitionKind);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Match_OperandCallInsideAssertion_IsNotCountedAsAssertion()
    {
        var fixture = AssertionLineageTestWorkspace.Create(
            "var actual = new Product.Service().GetName(); Xunit.Assert.True(actual.Contains(\"a\"));",
            productionMembers: "public string GetName() => \"abc\";");
        var (model, _) = await fixture.GetTestMethodAsync();
        var syntax = await fixture.GetInvocationAsync("Contains");
        var operation = Assert.IsAssignableFrom<IInvocationOperation>(model.GetOperation(syntax));

        Assert.Null(AssertionPatternCatalog.Shared.Match(operation));
    }
}
