using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models.Generation;
using TestMap.Services.TestGeneration.Construction;

namespace TestMap.Services.TestGeneration.TargetSelection;

public sealed class CandidateMethodMetadataService : ICandidateMethodMetadataService
{
    public CandidateMethodMetadata Build(string methodSource, string containingClassSource)
    {
        var method = ParseMethod(methodSource);
        var containingClass = ParseClass(containingClassSource);
        var intentions = method == null ? [] : BuildIntentions(method);
        var constructionPlan = BuildTypeConstructionPlan(method, containingClass);

        return new CandidateMethodMetadata
        {
            TestIntentions = intentions,
            TypeConstruction = constructionPlan,
            TestIntentionsSummary = BuildIntentionsSummary(intentions),
            TypeConstructionSummary = BuildConstructionSummary(constructionPlan)
        };
    }

    public async Task<CandidateMethodMetadata> BuildAsync(
        Document? document,
        int sourceStartPosition,
        int sourceEndPosition,
        string methodSource,
        string containingClassSource,
        CancellationToken cancellationToken = default)
    {
        if (document == null) return Build(methodSource, containingClassSource);

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root == null || semanticModel == null) return Build(methodSource, containingClassSource);

        var documentMethod = ResolveMethod(root, sourceStartPosition, sourceEndPosition);
        var method = documentMethod ?? ParseMethod(methodSource);
        if (method == null) return Build(methodSource, containingClassSource);

        var containingClass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>() ?? ParseClass(containingClassSource);
        var methodSymbol = documentMethod == null
            ? null
            : semanticModel.GetDeclaredSymbol(documentMethod, cancellationToken) as IMethodSymbol;
        var intentions = BuildIntentions(method);
        var constructionPlan = methodSymbol == null
            ? BuildTypeConstructionPlan(method, containingClass)
            : BuildSemanticTypeConstructionPlan(methodSymbol);

        return new CandidateMethodMetadata
        {
            TestIntentions = intentions,
            TypeConstruction = constructionPlan,
            TestIntentionsSummary = BuildIntentionsSummary(intentions),
            TypeConstructionSummary = BuildConstructionSummary(constructionPlan)
        };
    }

    private static MethodDeclarationSyntax? ParseMethod(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;

        var member = SyntaxFactory.ParseMemberDeclaration(source);
        if (member is MethodDeclarationSyntax method) return method;

        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
    }

    private static ClassDeclarationSyntax? ParseClass(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;

        var member = SyntaxFactory.ParseMemberDeclaration(source);
        if (member is ClassDeclarationSyntax sourceClass) return sourceClass;

        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
    }

    private static MethodDeclarationSyntax? ResolveMethod(
        SyntaxNode root,
        int sourceStartPosition,
        int sourceEndPosition)
    {
        return root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method =>
                sourceStartPosition <= 0 ||
                method.SpanStart <= sourceStartPosition && method.Span.End >= Math.Max(sourceStartPosition, sourceEndPosition))
            .OrderBy(method => method.Span.Length)
            .FirstOrDefault();
    }

    private static IReadOnlyList<CandidateTestIntention> BuildIntentions(MethodDeclarationSyntax method)
    {
        var intentions = new List<CandidateTestIntention>();

        foreach (var node in method.DescendantNodes())
        {
            switch (node)
            {
                case IfStatementSyntax ifStatement:
                    if (TryBuildGuardedThrowIntention(ifStatement, out var guardedThrow))
                    {
                        AddIntention(
                            intentions,
                            guardedThrow.Kind,
                            guardedThrow.SourceText,
                            guardedThrow.Intention);
                        // Emit the complementary happy-path branch so the LLM covers both sides.
                        AddIntention(
                            intentions,
                            "Branch",
                            ifStatement.Condition.ToString(),
                            $"Tests the path where `{ifStatement.Condition}` is false (no exception thrown).");
                    }
                    else
                    {
                        AddIntention(
                            intentions,
                            "Branch",
                            ifStatement.Condition.ToString(),
                            $"Tests the branch where `{ifStatement.Condition}` is true.");
                        AddIntention(
                            intentions,
                            "Branch",
                            ifStatement.Condition.ToString(),
                            $"Tests the branch where `{ifStatement.Condition}` is false.");
                        if (IsNullGuard(ifStatement.Condition))
                            AddIntention(
                                intentions,
                                "NullGuard",
                                ifStatement.Condition.ToString(),
                                $"Tests the null guard `{ifStatement.Condition}`.");
                    }
                    break;
                case ConditionalExpressionSyntax conditional:
                    AddIntention(
                        intentions,
                        "ConditionalExpression",
                        conditional.Condition.ToString(),
                        $"Tests the conditional expression when `{conditional.Condition}` is true.");
                    AddIntention(
                        intentions,
                        "ConditionalExpression",
                        conditional.Condition.ToString(),
                        $"Tests the conditional expression when `{conditional.Condition}` is false.");
                    break;
                case SwitchStatementSyntax switchStatement:
                    foreach (var label in switchStatement.Sections.SelectMany(section => section.Labels))
                        AddIntention(
                            intentions,
                            "SwitchCase",
                            label.ToString(),
                            $"Tests switch path `{label}`.");
                    break;
                case SwitchExpressionSyntax switchExpression:
                    foreach (var arm in switchExpression.Arms)
                        AddIntention(
                            intentions,
                            "SwitchArm",
                            arm.Pattern.ToString(),
                            $"Tests switch expression arm `{arm.Pattern}`.");
                    break;
                case ThrowStatementSyntax throwStatement when throwStatement.FirstAncestorOrSelf<IfStatementSyntax>() == null:
                    AddIntention(
                        intentions,
                        "ThrowPath",
                        throwStatement.Expression?.ToString() ?? "throw",
                        "Tests the method's explicit exception path.");
                    break;
                case ThrowExpressionSyntax throwExpression:
                    AddIntention(
                        intentions,
                        "ThrowExpression",
                        throwExpression.Expression.ToString(),
                        "Tests the expression-level exception path.");
                    break;
                case ConditionalAccessExpressionSyntax conditionalAccess:
                    // obj?.Member — implicit null-check: the expression is skipped when the
                    // receiver is null. A test should cover both the null and non-null receiver.
                    AddIntention(
                        intentions,
                        "NullableAccess",
                        conditionalAccess.Expression.ToString(),
                        $"Tests the nullable access on `{conditionalAccess.Expression}` — verifies behavior when the receiver is null.");
                    break;
                case BinaryExpressionSyntax binary when IsNullGuard(binary):
                    AddIntention(
                        intentions,
                        "NullGuard",
                        binary.ToString(),
                        $"Tests the null guard `{binary}`.");
                    break;
            }
        }

        AddEarlyReturnIntentions(method, intentions);

        return intentions
            .GroupBy(x => new { x.Kind, x.SourceText, x.Intention })
            .Select(x => x.First())
            .Take(16)
            .ToList();
    }

    private static void AddEarlyReturnIntentions(
        MethodDeclarationSyntax method,
        List<CandidateTestIntention> intentions)
    {
        var statements = method.Body?.Statements;
        if (statements == null || statements.Value.Count < 2) return;

        for (var index = 0; index < statements.Value.Count - 1; index++)
        {
            if (statements.Value[index] is ReturnStatementSyntax returnStatement)
                AddIntention(
                    intentions,
                    "EarlyReturn",
                    returnStatement.Expression?.ToString() ?? "return",
                    $"Tests early return path `{returnStatement.Expression?.ToString() ?? "return"}`.");
        }
    }

    private static bool TryBuildGuardedThrowIntention(
        IfStatementSyntax ifStatement,
        out CandidateTestIntention intention)
    {
        var throwStatement = ifStatement.Statement
            .DescendantNodesAndSelf()
            .OfType<ThrowStatementSyntax>()
            .FirstOrDefault();
        if (throwStatement?.Expression is not ObjectCreationExpressionSyntax objectCreation)
        {
            intention = new CandidateTestIntention();
            return false;
        }

        var exceptionType = objectCreation.Type.ToString();
        var message = objectCreation.ArgumentList?.Arguments
            .Select(argument => argument.Expression)
            .OfType<LiteralExpressionSyntax>()
            .FirstOrDefault(x => x.IsKind(SyntaxKind.StringLiteralExpression))
            ?.Token.ValueText;
        var parameterName = objectCreation.ArgumentList?.Arguments
            .Select(argument => argument.Expression)
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(x => x.Expression.ToString() == "nameof")
            ?.ArgumentList.Arguments.FirstOrDefault()
            ?.Expression.ToString();
        var suffix = !string.IsNullOrWhiteSpace(message)
            ? $" with message \"{message}\""
            : !string.IsNullOrWhiteSpace(parameterName)
                ? $" for {parameterName}"
                : string.Empty;

        intention = new CandidateTestIntention
        {
            Kind = IsNullGuard(ifStatement.Condition) ? "NullGuardThrowPath" : "GuardedThrowPath",
            SourceText = ifStatement.Condition.ToString(),
            Intention = $"Tests that `{ifStatement.Condition}` throws {exceptionType}{suffix}."
        };
        return true;
    }

    private static bool IsNullGuard(ExpressionSyntax expression)
    {
        return expression switch
        {
            BinaryExpressionSyntax binary =>
                binary.OperatorToken.Text is "==" or "is" &&
                (binary.Left.Kind() == SyntaxKind.NullLiteralExpression ||
                 binary.Right.Kind() == SyntaxKind.NullLiteralExpression),
            IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax constantPattern } =>
                constantPattern.Expression.Kind() == SyntaxKind.NullLiteralExpression,
            _ => false
        };
    }

    private static void AddIntention(
        ICollection<CandidateTestIntention> intentions,
        string kind,
        string sourceText,
        string intention)
    {
        intentions.Add(new CandidateTestIntention
        {
            Kind = kind,
            SourceText = sourceText,
            Intention = intention
        });
    }

    private static CandidateTypeConstructionPlan BuildTypeConstructionPlan(
        MethodDeclarationSyntax? method,
        ClassDeclarationSyntax? containingClass)
    {
        var requiresSutInstance = method == null || !method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword));
        var sutDependencies = requiresSutInstance
            ? SelectConstructor(containingClass)
                ?.ParameterList.Parameters
                .Select(parameter => BuildConstructionItem(parameter.Identifier.Text, parameter.Type?.ToString() ?? string.Empty, 0))
                .ToList() ?? []
            : [];
        var methodParameters = method?.ParameterList.Parameters
            .Select(parameter => BuildConstructionItem(parameter.Identifier.Text, parameter.Type?.ToString() ?? string.Empty, 0))
            .ToList() ?? [];

        return new CandidateTypeConstructionPlan
        {
            SutTypeName = containingClass?.Identifier.Text ?? string.Empty,
            RequiresSutInstance = requiresSutInstance,
            SutDependencies = sutDependencies,
            MethodParameters = methodParameters
        };
    }

    private static CandidateTypeConstructionPlan BuildSemanticTypeConstructionPlan(IMethodSymbol methodSymbol)
    {
        var containingType = methodSymbol.ContainingType;
        var constructor = containingType.InstanceConstructors
            .Where(x => !x.IsStatic && x.DeclaredAccessibility != Accessibility.Private)
            .OrderBy(x => x.Parameters.Length)
            .FirstOrDefault()
            ?? containingType.InstanceConstructors
                .Where(x => !x.IsStatic)
                .OrderBy(x => x.Parameters.Length)
                .FirstOrDefault();

        return new CandidateTypeConstructionPlan
        {
            SutTypeName = containingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            RequiresSutInstance = !methodSymbol.IsStatic,
            SutDependencies = methodSymbol.IsStatic
                ? []
                : (constructor?.Parameters
                    .Select(parameter => BuildSemanticConstructionItem(parameter.Name, parameter.Type, 0, EmptyVisitedTypes()))
                    .ToList() ?? []),
            MethodParameters = methodSymbol.Parameters
                .Select(parameter => BuildSemanticConstructionItem(parameter.Name, parameter.Type, 0, EmptyVisitedTypes()))
                .ToList()
        };
    }

    private static IReadOnlySet<string> EmptyVisitedTypes()
    {
        return new HashSet<string>(StringComparer.Ordinal);
    }

    private static CandidateTypeConstructionItem BuildSemanticConstructionItem(
        string name,
        ITypeSymbol type,
        int depth,
        IReadOnlySet<string> visitedTypeKeys)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var advice = TestInputConstructionAdvisor.ForSymbol(type, name);
        var key = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (visitedTypeKeys.Contains(key))
            return new CandidateTypeConstructionItem
            {
                Name = name,
                TypeName = typeName,
                Strategy = "mock",
                Summary = $"{name}: mock {typeName} to break a circular construction dependency."
            };

        var nextVisited = visitedTypeKeys.Append(key).ToHashSet(StringComparer.Ordinal);
        var children = depth >= 4 ? [] : BuildSemanticChildren(type, depth + 1, nextVisited);

        return new CandidateTypeConstructionItem
        {
            Name = name,
            TypeName = typeName,
            Strategy = advice.Strategy,
            Summary = advice.Summary,
            Children = children
        };
    }

    private static IReadOnlyList<CandidateTypeConstructionItem> BuildSemanticChildren(
        ITypeSymbol type,
        int depth,
        IReadOnlySet<string> visitedTypeKeys)
    {
        if (TestInputConstructionAdvisor.TryResolveElementType(type, out var elementType))
            return [BuildSemanticConstructionItem("item", elementType, depth, visitedTypeKeys)];

        if (type is not INamedTypeSymbol namedType ||
            TestInputConstructionAdvisor.ForSymbol(type, string.Empty).Strategy != "construct" ||
            IsFrameworkType(type))
            return [];

        var constructor = namedType.InstanceConstructors
            .Where(x => !x.IsStatic && x.DeclaredAccessibility != Accessibility.Private)
            .OrderBy(x => x.Parameters.Length)
            .FirstOrDefault();

        return constructor?.Parameters
            .Select(parameter => BuildSemanticConstructionItem(parameter.Name, parameter.Type, depth, visitedTypeKeys))
            .ToList() ?? [];
    }

    private static bool IsFrameworkType(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns.StartsWith("System", StringComparison.Ordinal) ||
               ns.StartsWith("Microsoft", StringComparison.Ordinal);
    }

    private static ConstructorDeclarationSyntax? SelectConstructor(ClassDeclarationSyntax? containingClass)
    {
        return containingClass?.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(x => !x.Modifiers.Any(modifier => modifier.RawKind == (int)SyntaxKind.StaticKeyword))
            .OrderBy(x => x.ParameterList.Parameters.Count)
            .FirstOrDefault();
    }

    private static CandidateTypeConstructionItem BuildConstructionItem(
        string name,
        string typeName,
        int depth)
    {
        var advice = TestInputConstructionAdvisor.ForTypeName(typeName, name);
        var children = depth >= 2 ? [] : BuildCollectionChildren(typeName, depth + 1);

        return new CandidateTypeConstructionItem
        {
            Name = name,
            TypeName = typeName,
            Strategy = advice.Strategy,
            Summary = advice.Summary,
            Children = children
        };
    }

    private static IReadOnlyList<CandidateTypeConstructionItem> BuildCollectionChildren(string typeName, int depth)
    {
        var elementType = ExtractCollectionElementType(typeName);
        if (string.IsNullOrWhiteSpace(elementType)) return [];

        return [BuildConstructionItem("item", elementType, depth)];
    }

    private static string? ExtractCollectionElementType(string typeName)
    {
        var normalized = TestInputConstructionAdvisor.NormalizeTypeName(typeName);
        if (normalized.EndsWith("[]", StringComparison.Ordinal))
            return normalized[..^2];

        var start = normalized.IndexOf('<');
        var end = normalized.LastIndexOf('>');
        return start >= 0 && end > start ? normalized[(start + 1)..end].Split(',')[0].Trim() : null;
    }

    private static string BuildIntentionsSummary(IReadOnlyList<CandidateTestIntention> intentions)
    {
        if (intentions.Count == 0) return "No branch-derived test intentions were identified.";

        return string.Join(Environment.NewLine, intentions.Select(x => $"- {x.Intention}"));
    }

    private static string BuildConstructionSummary(CandidateTypeConstructionPlan plan)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(plan.SutTypeName))
            lines.Add(plan.RequiresSutInstance
                ? $"SUT: {plan.SutTypeName}"
                : $"SUT: static call target {plan.SutTypeName}; no SUT construction required.");
        lines.AddRange(plan.SutDependencies.Select(x => $"- SUT dependency {x.Summary}"));
        lines.AddRange(plan.MethodParameters.Select(x => $"- Method parameter {x.Summary}"));

        return lines.Count == 0
            ? "No type construction guidance was identified."
            : string.Join(Environment.NewLine, lines);
    }
}
