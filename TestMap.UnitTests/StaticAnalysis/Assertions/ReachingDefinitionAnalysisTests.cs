using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TestMap.Services.StaticAnalysis.Assertions;

namespace TestMap.UnitTests.StaticAnalysis.Assertions;

public sealed class ReachingDefinitionAnalysisTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetReachingDefinitions_UsesLatestDefinitionAtAssertion()
    {
        var fixture = AssertionLineageTestWorkspace.Create(
            "var value = 1; value = 2; Xunit.Assert.Equal(2, value);");
        var (model, method) = await fixture.GetTestMethodAsync();
        var graph = Assert.IsType<ControlFlowGraph>(
            ControlFlowGraph.Create(method, model));
        var invocationSyntax = await fixture.GetInvocationAsync("Equal");
        var invocation = Assert.IsAssignableFrom<IInvocationOperation>(
            model.GetOperation(invocationSyntax));
        var local = Assert.IsAssignableFrom<ILocalReferenceOperation>(
            invocation.Arguments[1].Value);

        var definitions = new ReachingDefinitionAnalysis(graph)
            .GetReachingDefinitions(local.Local, local);

        var definition = Assert.Single(definitions);
        Assert.Equal(2, definition.Value!.ConstantValue.Value);
    }
}
