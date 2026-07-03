using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models.Generation;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestGeneration.Construction;

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
            var abstractType = parameter.Type;
            var requiresMocking = RequiresMocking(abstractType);
            // Only look for a concrete stand-in when mocking is the current outcome AND the
            // type is a class (not an interface, and type-token types like System.Type are
            // already excluded because RequiresMocking returns false for them).
            var concreteSubtype = requiresMocking && abstractType.TypeKind == TypeKind.Class
                ? FindConcreteSubtype(abstractType, semanticModel.Compilation)
                : null;
            var resolvedType = (ITypeSymbol?)concreteSubtype ?? abstractType;
            var typeName = resolvedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            requiresMocking = concreteSubtype == null && requiresMocking;
            nodes.Add(new ContextGraphNode
            {
                NodeId = $"param:{parameter.Name}",
                NodeType = "MethodParameter",
                TypeName = typeName,
                VariableName = parameter.Name,
                SourceSummary = $"Method parameter {parameter.Name} of type {abstractType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}.",
                ConstructionHint = concreteSubtype != null
                    ? BuildAbstractSubstitutionHint(abstractType, concreteSubtype, parameter.Name)
                    : BuildConstructionHint(abstractType, parameter.Name),
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
                NodeType = methodSymbol.IsStatic ? "StaticCallTarget" : "SystemUnderTest",
                TypeName = typeName,
                VariableName = methodSymbol.IsStatic ? null : "sut",
                DependsOnNodeIds = methodSymbol.IsStatic
                    ? []
                    : methodSymbol.Parameters.Select(x => $"param:{x.Name}").ToList(),
                SourceSummary = $"Containing type {typeName}.",
                ConstructionHint = methodSymbol.IsStatic
                    ? $"Call {typeName}.{methodSymbol.Name}(...) directly; no SUT instance is required."
                    : BuildConstructorHint(containingType),
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
                ConstructionHint = IsNoHelpersFoundMessage(hint)
                    ? "No existing helper found; construct all test dependencies from scratch."
                    : "Reuse this existing fixture/setup helper when it matches the scenario.",
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
            var isExpectedException = IsExpectedExceptionCreation(creation);
            yield return new ContextGraphNode
            {
                NodeId = $"creates:{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}",
                NodeType = isExpectedException ? "ExpectedException" : "ConstructedDependency",
                TypeName = typeName,
                SourceSummary = isExpectedException
                    ? $"Method body throws {typeName}."
                    : $"Method body creates {typeName}.",
                ConstructionHint = isExpectedException
                    ? $"Assert the thrown {typeName} when targeting this guard path; do not arrange it as an input dependency."
                    : $"The method already constructs {typeName}; avoid duplicating setup unless needed.",
                RequiresMocking = false,
                IsResolved = true
            };
        }
    }

    private static bool IsExpectedExceptionCreation(ObjectCreationExpressionSyntax creation)
    {
        return creation.FirstAncestorOrSelf<ThrowStatementSyntax>() != null ||
               creation.FirstAncestorOrSelf<ThrowExpressionSyntax>() != null;
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

        return TestInputConstructionAdvisor.ForSymbol(type, name).Summary;
    }

    private static bool RequiresMocking(ITypeSymbol type)
    {
        if (TestInputConstructionAdvisor.ForSymbol(type, string.Empty).Strategy == "type-token")
            return false;

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
            var isStaticMethod = IsStaticMethodSignature(request.MethodSignature) ||
                                 IsStaticClass(request.ContainingClass, className);
            nodes.Add(new ContextGraphNode
            {
                NodeId = "sut",
                NodeType = isStaticMethod ? "StaticCallTarget" : "SystemUnderTest",
                TypeName = className,
                VariableName = isStaticMethod ? null : "sut",
                DependsOnNodeIds = isStaticMethod
                    ? []
                    : parameters.Select(x => $"param:{x.Name}").ToList(),
                SourceSummary = $"Containing class {className}.",
                ConstructionHint = isStaticMethod
                    ? $"Call {className}.{request.MethodName}(...) directly; no SUT instance is required."
                    : FindConstructorHint(request.ContainingClass, className),
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
                ConstructionHint = IsNoHelpersFoundMessage(hint)
                    ? "No existing helper found; construct all test dependencies from scratch."
                    : "Reuse this existing fixture/setup helper when it matches the scenario.",
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

    private static bool IsStaticMethodSignature(string methodSignature)
    {
        return Regex.IsMatch(methodSignature, @"\bstatic\b");
    }

    private static bool IsStaticClass(string containingClass, string className)
    {
        return Regex.IsMatch(containingClass, $@"\bstatic\s+class\s+{Regex.Escape(className)}\b");
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
        return TestInputConstructionAdvisor.RequiresMocking(typeName);
    }

    private static string BuildConstructionHint(string typeName, string name)
    {
        return TestInputConstructionAdvisor.ForTypeName(typeName, name).Summary;
    }

    /// <summary>
    /// For abstract class parameters, searches the Roslyn compilation for the simplest
    /// non-abstract public subtype. Returns null for interfaces or non-abstract types.
    /// </summary>
    private static INamedTypeSymbol? FindConcreteSubtype(ITypeSymbol abstractType, Compilation compilation)
    {
        if (abstractType.TypeKind == TypeKind.Interface || !abstractType.IsAbstract)
            return null;

        return GetAllNamedTypes(compilation.GlobalNamespace)
            .Where(t => !t.IsAbstract
                     && !t.IsGenericType
                     && t.DeclaredAccessibility == Accessibility.Public
                     && InheritsFrom(t, abstractType)
                     && t.InstanceConstructors.Any(c =>
                         !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public))
            .OrderBy(ConcreteSubtypeScore)
            .FirstOrDefault();
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
            yield return type;
        foreach (var child in ns.GetNamespaceMembers())
            foreach (var type in GetAllNamedTypes(child))
                yield return type;
    }

    private static bool InheritsFrom(INamedTypeSymbol candidate, ITypeSymbol target)
    {
        var baseType = candidate.BaseType;
        while (baseType != null)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, target) ||
                SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, target.OriginalDefinition))
                return true;
            baseType = baseType.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Scores a concrete subtype candidate: prefers the fewest required constructor parameters,
    /// with a large penalty for any parameter that is itself abstract or interface-typed.
    /// </summary>
    private static int ConcreteSubtypeScore(INamedTypeSymbol type)
    {
        var ctor = type.InstanceConstructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(c => c.Parameters.Length)
            .FirstOrDefault();
        if (ctor == null) return int.MaxValue;

        var complexParams = ctor.Parameters.Count(p =>
            p.Type.TypeKind == TypeKind.Interface ||
            p.Type.IsAbstract ||
            (p.Type.TypeKind == TypeKind.Class &&
             p.Type.SpecialType == SpecialType.None &&
             !IsKnownFrameworkSimpleType(p.Type)));

        return ctor.Parameters.Length + complexParams * 100;
    }

    private static bool IsKnownFrameworkSimpleType(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None) return true;
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is
            "global::System.String" or
            "global::System.DateTime" or
            "global::System.Guid" or
            "global::System.TimeSpan" or
            "global::System.DateOnly" or
            "global::System.TimeOnly";
    }

    private static string BuildAbstractSubstitutionHint(
        ITypeSymbol abstractType,
        INamedTypeSymbol concreteType,
        string name)
    {
        var abstractName = abstractType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var concreteName = concreteType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var ctor = concreteType.InstanceConstructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(c => c.Parameters.Length)
            .FirstOrDefault();
        var ctorParams = ctor?.Parameters.Length == 0
            ? string.Empty
            : ctor == null
                ? "..."
                : string.Join(", ", ctor.Parameters.Select(p =>
                    $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"));
        return $"Concrete stand-in for abstract {abstractName}: construct as new {concreteName}({ctorParams}).";
    }

    private static bool IsNoHelpersFoundMessage(string hint)
    {
        return hint.StartsWith("No ", StringComparison.OrdinalIgnoreCase) ||
               hint.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
               hint.Contains("none found", StringComparison.OrdinalIgnoreCase) ||
               hint.Contains("no helpers", StringComparison.OrdinalIgnoreCase) ||
               hint.Contains("no setup", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ParameterInfo(string TypeName, string Name);
}
