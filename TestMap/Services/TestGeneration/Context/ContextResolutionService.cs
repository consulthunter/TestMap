using TestMap.Models.Generation;
using TestMap.Services.TestGeneration.Construction;

namespace TestMap.Services.TestGeneration.Context;

public sealed class ContextResolutionService : IContextResolutionService
{
    public IReadOnlyList<ContextResolutionResult> Resolve(ContextGraph graph)
    {
        return graph.Nodes.Select(ResolveNode).ToList();
    }

    private static ContextResolutionResult ResolveNode(ContextGraphNode node)
    {
        if (node.RequiresMocking)
        {
            return new ContextResolutionResult
            {
                NodeId = node.NodeId,
                Success = true,
                CodeSnippet = $"// Arrange a mock or fake for {node.TypeName} {node.VariableName}".TrimEnd(),
                Explanation = "Interface-like dependency should be mocked, faked, or reused from fixtures."
            };
        }

        if (node.NodeType == "MethodParameter" && !string.IsNullOrWhiteSpace(node.VariableName))
        {
            return new ContextResolutionResult
            {
                NodeId = node.NodeId,
                Success = true,
                CodeSnippet = BuildParameterSnippet(node.TypeName, node.VariableName),
                Explanation = node.ConstructionHint
            };
        }

        if (node.NodeType == "SystemUnderTest")
        {
            return new ContextResolutionResult
            {
                NodeId = node.NodeId,
                Success = true,
                CodeSnippet = $"var sut = new {node.TypeName}();",
                Explanation = node.ConstructionHint
            };
        }

        if (node.NodeType == "StaticCallTarget")
        {
            return new ContextResolutionResult
            {
                NodeId = node.NodeId,
                Success = true,
                CodeSnippet = string.Empty,
                Explanation = node.ConstructionHint
            };
        }

        return new ContextResolutionResult
        {
            NodeId = node.NodeId,
            Success = true,
            CodeSnippet = string.Empty,
            Explanation = node.ConstructionHint
        };
    }

    private static string BuildParameterSnippet(string typeName, string variableName)
    {
        return TestInputConstructionAdvisor.BuildParameterSnippet(typeName, variableName);
    }
}
