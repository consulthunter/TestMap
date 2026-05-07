using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models.Generation;
using TestMap.Services.StaticAnalysis;

namespace TestMap.Services.TestGeneration.Context;

public sealed class ContextGraphService : IContextGraphService
{
    private readonly IStaticAnalysisWorkspace? _staticAnalysisWorkspace;

    public ContextGraphService(IStaticAnalysisWorkspace? staticAnalysisWorkspace = null)
    {
        _staticAnalysisWorkspace = staticAnalysisWorkspace;
    }

    public async Task<ContextGraph> BuildAsync(
        TestGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_staticAnalysisWorkspace != null &&
            !string.IsNullOrWhiteSpace(request.SourceFilePath) &&
            (!string.IsNullOrWhiteSpace(request.SourceProjectPath) ||
             !string.IsNullOrWhiteSpace(request.SolutionFilePath)))
        {
            var graph = await TryBuildFromRoslynDocumentAsync(request, cancellationToken);
            if (graph != null) return graph;
        }

        return BuildFromRequestSnippets(request);
    }

    private async Task<ContextGraph?> TryBuildFromRoslynDocumentAsync(
        TestGenerationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await OpenProjectAsync(request, cancellationToken);
            if (project == null) return null;

            var document = project.Documents.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.FilePath) &&
                string.Equals(Path.GetFullPath(x.FilePath), Path.GetFullPath(request.SourceFilePath),
                    StringComparison.OrdinalIgnoreCase));
            if (document == null) return null;

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (root == null || semanticModel == null) return null;

            var methodDeclaration = FindTargetMethod(root, request);
            if (methodDeclaration == null) return null;

            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken) as IMethodSymbol;
            if (methodSymbol == null) return null;

            return BuildFromSymbols(request, methodDeclaration, methodSymbol, semanticModel);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Project?> OpenProjectAsync(
        TestGenerationRequest request,
        CancellationToken cancellationToken)
    {
        if (_staticAnalysisWorkspace == null) return null;

        if (!string.IsNullOrWhiteSpace(request.SolutionFilePath) && File.Exists(request.SolutionFilePath))
        {
            var solution = await _staticAnalysisWorkspace.OpenSolutionAsync(request.SolutionFilePath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.SourceProjectPath))
                return solution.Projects.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.FilePath) &&
                    string.Equals(Path.GetFullPath(x.FilePath), Path.GetFullPath(request.SourceProjectPath),
                        StringComparison.OrdinalIgnoreCase));

            return solution.Projects.FirstOrDefault(x => x.Documents.Any(document =>
                !string.IsNullOrWhiteSpace(document.FilePath) &&
                string.Equals(Path.GetFullPath(document.FilePath), Path.GetFullPath(request.SourceFilePath),
                    StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(request.SourceProjectPath) && File.Exists(request.SourceProjectPath))
            return await _staticAnalysisWorkspace.OpenProjectAsync(request.SourceProjectPath, cancellationToken);

        return null;
    }

    private static MethodDeclarationSyntax? FindTargetMethod(SyntaxNode root, TestGenerationRequest request)
    {
        var candidates = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(x => string.Equals(x.Identifier.Text, request.MethodName, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0) return null;

        if (request.SourceStartLine > 0 || request.SourceEndLine > 0)
        {
            var byLocation = candidates.FirstOrDefault(method =>
            {
                var span = method.SyntaxTree.GetLineSpan(method.Span);
                return LinesOverlap(
                    span.StartLinePosition.Line,
                    span.EndLinePosition.Line,
                    request.SourceStartLine,
                    request.SourceEndLine);
            });
            if (byLocation != null) return byLocation;
        }

        return candidates.First();
    }

    private static bool LinesOverlap(
        int methodStartLine,
        int methodEndLine,
        int candidateStartLine,
        int candidateEndLine)
    {
        var start = Math.Max(0, candidateStartLine);
        var end = Math.Max(start, candidateEndLine);
        return methodStartLine <= end && start <= methodEndLine;
    }

    private static ContextGraph BuildFromSymbols(
        TestGenerationRequest request,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        var nodes = new List<ContextGraphNode>();

        foreach (var parameter in methodSymbol.Parameters)
        {
            var typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var requiresMocking = RequiresMocking(parameter.Type);
            nodes.Add(new ContextGraphNode
            {
                NodeId = $"param:{parameter.Name}",
                NodeType = "MethodParameter",
                TypeName = typeName,
                VariableName = parameter.Name,
                SourceSummary = $"Method parameter {parameter.Name} of type {typeName}.",
                ConstructionHint = BuildConstructionHint(parameter.Type, parameter.Name),
                RequiresMocking = requiresMocking,
                IsResolved = !requiresMocking
            });
        }

        var containingType = methodSymbol.ContainingType;
        if (containingType != null)
        {
            var typeName = containingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            nodes.Add(new ContextGraphNode
            {
                NodeId = "sut",
                NodeType = "SystemUnderTest",
                TypeName = typeName,
                VariableName = "sut",
                DependsOnNodeIds = methodSymbol.Parameters.Select(x => $"param:{x.Name}").ToList(),
                SourceSummary = $"Containing type {typeName}.",
                ConstructionHint = BuildConstructorHint(containingType),
                RequiresMocking = false,
                IsResolved = true
            });

            foreach (var factory in containingType.GetMembers()
                         .OfType<IMethodSymbol>()
                         .Where(x => x.IsStatic &&
                                     x.DeclaredAccessibility == Accessibility.Public &&
                                     SymbolEqualityComparer.Default.Equals(x.ReturnType, containingType)))
            {
                nodes.Add(new ContextGraphNode
                {
                    NodeId = $"factory:{factory.Name}",
                    NodeType = "StaticFactory",
                    TypeName = typeName,
                    SourceSummary = $"Public static factory {factory.Name} returns {typeName}.",
                    ConstructionHint = $"Prefer {typeName}.{factory.Name}(...) when it matches the scenario.",
                    RequiresMocking = false,
                    IsResolved = true
                });
            }
        }

        foreach (var dependency in FindBodyDependencies(methodDeclaration, semanticModel))
        {
            if (nodes.Any(x => x.NodeId == dependency.NodeId)) continue;
            nodes.Add(dependency);
        }

        foreach (var hint in ExtractFixtureHints(request.TestSupportContext))
        {
            nodes.Add(new ContextGraphNode
            {
                NodeId = $"fixture:{nodes.Count}",
                NodeType = "FixtureHint",
                TypeName = string.Empty,
                SourceSummary = hint,
                ConstructionHint = "Reuse this existing fixture/setup helper when it matches the scenario.",
                RequiresMocking = false,
                IsResolved = true
            });
        }

        return new ContextGraph
        {
            CandidateId = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Nodes = nodes
        };
    }

    private static IEnumerable<ContextGraphNode> FindBodyDependencies(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel)
    {
        var objectCreations = methodDeclaration.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in objectCreations)
        {
            var type = semanticModel.GetTypeInfo(creation).Type;
            if (type == null) continue;

            var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            yield return new ContextGraphNode
            {
                NodeId = $"creates:{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}",
                NodeType = "ConstructedDependency",
                TypeName = typeName,
                SourceSummary = $"Method body creates {typeName}.",
                ConstructionHint = $"The method already constructs {typeName}; avoid duplicating setup unless needed.",
                RequiresMocking = false,
                IsResolved = true
            };
        }
    }

    private static string BuildConstructorHint(INamedTypeSymbol containingType)
    {
        var constructors = containingType.Constructors
            .Where(x => !x.IsStatic && x.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(x => x.Parameters.Length)
            .ToList();
        if (constructors.Count == 0)
            return $"Use existing fixture setup or accessible factory for {containingType.Name}.";

        var best = constructors[0];
        var parameters = best.Parameters.Length == 0
            ? "no arguments"
            : string.Join(", ", best.Parameters.Select(x =>
                $"{x.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {x.Name}"));
        return $"Construct {containingType.Name} with public constructor requiring {parameters}.";
    }

    private static string BuildConstructionHint(ITypeSymbol type, string name)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (RequiresMocking(type)) return $"Create a mock or fake for {typeName}.";

        return BuildConstructionHint(typeName, name);
    }

    private static bool RequiresMocking(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.Interface ||
               type.IsAbstract ||
               RequiresMocking(type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
    }

    private static ContextGraph BuildFromRequestSnippets(TestGenerationRequest request)
    {
        var nodes = new List<ContextGraphNode>();
        var parameters = ExtractParameters(request.MethodSignature);

        foreach (var parameter in parameters)
        {
            nodes.Add(new ContextGraphNode
            {
                NodeId = $"param:{parameter.Name}",
                NodeType = "MethodParameter",
                TypeName = parameter.TypeName,
                VariableName = parameter.Name,
                SourceSummary = $"Method parameter {parameter.Name} of type {parameter.TypeName}.",
                ConstructionHint = BuildConstructionHint(parameter.TypeName, parameter.Name),
                RequiresMocking = RequiresMocking(parameter.TypeName),
                IsResolved = !RequiresMocking(parameter.TypeName)
            });
        }

        var className = ExtractClassName(request.ContainingClass);
        if (!string.IsNullOrWhiteSpace(className))
        {
            nodes.Add(new ContextGraphNode
            {
                NodeId = "sut",
                NodeType = "SystemUnderTest",
                TypeName = className,
                VariableName = "sut",
                DependsOnNodeIds = parameters.Select(x => $"param:{x.Name}").ToList(),
                SourceSummary = $"Containing class {className}.",
                ConstructionHint = FindConstructorHint(request.ContainingClass, className),
                RequiresMocking = false,
                IsResolved = true
            });

            foreach (var factory in FindStaticFactories(request.ContainingClass, className))
            {
                nodes.Add(new ContextGraphNode
                {
                    NodeId = $"factory:{factory}",
                    NodeType = "StaticFactory",
                    TypeName = className,
                    SourceSummary = $"Static factory {factory} returns {className}.",
                    ConstructionHint = $"Prefer {className}.{factory}(...) if constructor setup is noisy.",
                    RequiresMocking = false,
                    IsResolved = true
                });
            }
        }

        foreach (var hint in ExtractFixtureHints(request.TestSupportContext))
        {
            nodes.Add(new ContextGraphNode
            {
                NodeId = $"fixture:{nodes.Count}",
                NodeType = "FixtureHint",
                TypeName = string.Empty,
                SourceSummary = hint,
                ConstructionHint = "Reuse this existing fixture/setup helper when it matches the scenario.",
                RequiresMocking = false,
                IsResolved = true
            });
        }

        return new ContextGraph
        {
            CandidateId = request.MethodName,
            Nodes = nodes
        };
    }

    private static IReadOnlyList<ParameterInfo> ExtractParameters(string methodSignature)
    {
        var open = methodSignature.IndexOf('(');
        var close = methodSignature.LastIndexOf(')');
        if (open < 0 || close <= open) return [];

        return methodSignature[(open + 1)..close]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseParameter)
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.TypeName))
            .ToList();
    }

    private static ParameterInfo ParseParameter(string text)
    {
        var cleaned = text
            .Replace("this ", string.Empty, StringComparison.Ordinal)
            .Replace("in ", string.Empty, StringComparison.Ordinal)
            .Replace("out ", string.Empty, StringComparison.Ordinal)
            .Replace("ref ", string.Empty, StringComparison.Ordinal);
        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return new ParameterInfo(string.Empty, string.Empty);

        return new ParameterInfo(string.Join(" ", parts[..^1]), parts[^1]);
    }

    private static string ExtractClassName(string containingClass)
    {
        var match = Regex.Match(containingClass, @"\b(class|record)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)");
        return match.Success ? match.Groups["name"].Value : string.Empty;
    }

    private static string FindConstructorHint(string containingClass, string className)
    {
        return Regex.IsMatch(containingClass, $@"\bpublic\s+{Regex.Escape(className)}\s*\(")
            ? $"Construct {className} with its public constructor and required dependencies."
            : $"Construct or access {className} using available test fixture setup.";
    }

    private static IReadOnlyList<string> FindStaticFactories(string containingClass, string className)
    {
        return Regex.Matches(containingClass, $@"\bstatic\s+{Regex.Escape(className)}\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(")
            .Select(x => x.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> ExtractFixtureHints(string supportContext)
    {
        if (string.IsNullOrWhiteSpace(supportContext)) return [];

        return supportContext
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Contains("fixture", StringComparison.OrdinalIgnoreCase) ||
                        x.Contains("builder", StringComparison.OrdinalIgnoreCase) ||
                        x.Contains("factory", StringComparison.OrdinalIgnoreCase) ||
                        x.Contains("setup", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();
    }

    private static bool RequiresMocking(string typeName)
    {
        var normalized = typeName.Trim().TrimEnd('?');
        return normalized.StartsWith('I') &&
               normalized.Length > 1 &&
               char.IsUpper(normalized[1]) &&
               normalized != "Int32";
    }

    private static string BuildConstructionHint(string typeName, string name)
    {
        var normalized = typeName.Trim().TrimEnd('?');
        return normalized switch
        {
            "string" or "String" => $"Use a meaningful string value for {name}.",
            "int" or "Int32" or "long" or "Int64" => $"Use a simple numeric value for {name}.",
            "bool" or "Boolean" => $"Choose true or false for {name} based on the scenario.",
            _ when RequiresMocking(normalized) => $"Create a mock or fake for {normalized}.",
            _ => $"Construct {normalized} with the smallest valid test data."
        };
    }

    private sealed record ParameterInfo(string TypeName, string Name);
}
