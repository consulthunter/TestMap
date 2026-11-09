using Microsoft.CodeAnalysis.Operations;
using TestMap.Services.StaticAnalysis.Assertions;

namespace TestMap.UnitTests.StaticAnalysis.Assertions;

public sealed class AssertionOperandExtractorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Extract_XunitEqual_IncludesExpectedAndActualButNotMessage()
    {
        var fixture = AssertionLineageTestWorkspace.Create(
            "Xunit.Assert.Equal(1, new Product.Service().GetValue(), \"diagnostic\");");
        var (model, _) = await fixture.GetTestMethodAsync();
        var syntax = await fixture.GetInvocationAsync("Equal");
        var operation = Assert.IsAssignableFrom<IInvocationOperation>(model.GetOperation(syntax));
        var match = Assert.IsType<AssertionPatternMatch>(
            AssertionPatternCatalog.Shared.Match(operation));

        var operands = new AssertionOperandExtractor().Extract(operation, match);

        Assert.Equal(2, operands.Count);
        Assert.Equal(["expected", "actual"], operands.Select(operand => operand.Role));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Extract_FluentBe_UnwrapsOriginalSubjectOnce()
    {
        var fixture = AssertionLineageTestWorkspace.Create(
            "new Product.Service().GetValue().Should().Be(1);");
        var (model, _) = await fixture.GetTestMethodAsync();
        var syntax = await fixture.GetInvocationAsync("Be");
        var operation = Assert.IsAssignableFrom<IInvocationOperation>(model.GetOperation(syntax));
        var match = Assert.IsType<AssertionPatternMatch>(
            AssertionPatternCatalog.Shared.Match(operation));

        var operands = new AssertionOperandExtractor().Extract(operation, match);

        Assert.Equal(2, operands.Count);
        Assert.Equal("subject", operands[0].Role);
        Assert.Contains("GetValue", operands[0].Operation.Syntax.ToString());
    }
}
