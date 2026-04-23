using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using TestMap.App;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Repositories.Code;

namespace TestMap.Services.StaticAnalysis;

using CodeLocation = TestMap.Models.Code.Location;
using RoslynProject = Microsoft.CodeAnalysis.Project;

public class AnalyzeProjectService : IAnalyzeProjectService
{
    private readonly ProjectContext _context;
    private readonly CSharpProjectRepository _cSharpProjectRepository;
    private readonly CSharpSolutionRepository _cSharpSolutionRepository;
    private readonly FileRepository _fileRepository;
    private readonly ObjectRepository _objectRepository;
    private readonly MemberRepository _memberRepository;
    private readonly ObjectRelationshipRepository _objectRelationshipRepository;
    private readonly MemberRelationshipRepository _memberRelationshipRepository;
    private readonly InvocationRepository _invocationRepository;
    private readonly IStaticAnalysisWorkspace _staticAnalysisWorkspace;

    public AnalyzeProjectService(
        ProjectContext context,
        CSharpProjectRepository cSharpProjectRepository,
        CSharpSolutionRepository cSharpSolutionRepository,
        FileRepository fileRepository,
        ObjectRepository objectRepository,
        MemberRepository memberRepository,
        ObjectRelationshipRepository objectRelationshipRepository,
        MemberRelationshipRepository memberRelationshipRepository,
        InvocationRepository invocationRepository,
        IStaticAnalysisWorkspace staticAnalysisWorkspace)
    {
        _context = context;
        _cSharpProjectRepository = cSharpProjectRepository;
        _cSharpSolutionRepository = cSharpSolutionRepository;
        _fileRepository = fileRepository;
        _objectRepository = objectRepository;
        _memberRepository = memberRepository;
        _objectRelationshipRepository = objectRelationshipRepository;
        _memberRelationshipRepository = memberRelationshipRepository;
        _invocationRepository = invocationRepository;
        _staticAnalysisWorkspace = staticAnalysisWorkspace;
    }

    public async Task AnalyzeProjectAsync(CSharpProjectModel analysisProject)
    {
        await EnsureProjectPersistedAsync(analysisProject);

        var project = await _staticAnalysisWorkspace.OpenProjectAsync(analysisProject.FilePath);
        project = RemoveSourceGenerators(project);

        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
        {
            _context.Logger?.Warning("Compilation could not be created for {ProjectFilePath}", analysisProject.FilePath);
            return;
        }

        var projectDocumentPaths = new HashSet<string>(
            analysisProject.DocumentFilePaths.Where(path => !string.IsNullOrWhiteSpace(path)),
            StringComparer.OrdinalIgnoreCase);

        var state = new AnalysisState(projectDocumentPaths);

        foreach (var document in project.Documents)
        {
            if (!ShouldAnalyzeDocument(document.FilePath))
            {
                continue;
            }

            await AnalyzeDocumentAsync(document, compilation, analysisProject, state);
        }

        await PersistRelationshipsAsync(state);
    }

    private static RoslynProject RemoveSourceGenerators(RoslynProject project)
    {
        var solution = project.Solution;
        foreach (var solutionProject in solution.Projects)
        {
            if (solutionProject.AnalyzerReferences.Any())
            {
                solution = solution.WithProjectAnalyzerReferences(
                    solutionProject.Id,
                    []);
            }
        }

        return solution.GetProject(project.Id) ?? project;
    }

    private async Task EnsureProjectPersistedAsync(CSharpProjectModel analysisProject)
    {
        var solutionModel = _context.Project.Solutions.FirstOrDefault(x => x.Projects.Contains(analysisProject.FilePath));
        if (solutionModel == null)
        {
            throw new InvalidOperationException($"No solution mapping was found for {analysisProject.FilePath}.");
        }

        solutionModel.ProjectId = _context.Project.DbId;
        analysisProject.SolutionId = await _cSharpSolutionRepository.InsertOrUpdateAsync(solutionModel);
        solutionModel.Id = analysisProject.SolutionId;

        analysisProject.Id = await _cSharpProjectRepository.InsertOrUpdateAsync(analysisProject);
    }

    private async Task AnalyzeDocumentAsync(
        Document document,
        Compilation compilation,
        CSharpProjectModel analysisProject,
        AnalysisState state)
    {
        var root = await document.GetSyntaxRootAsync();
        if (root == null || document.FilePath == null)
        {
            return;
        }

        var syntaxTree = root.SyntaxTree;
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        var fileId = await EnsureFilePersistedAsync(root, analysisProject.Id, document.FilePath);
        var objectDeclarations = root.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Where(x => x.Parent is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax or CompilationUnitSyntax
                        || x.Parent is TypeDeclarationSyntax or RecordDeclarationSyntax)
            .ToList();

        foreach (var objectDeclaration in objectDeclarations)
        {
            var objectSymbol = semanticModel.GetDeclaredSymbol(objectDeclaration) as INamedTypeSymbol;
            if (objectSymbol == null)
            {
                continue;
            }

            var objectKey = GetSymbolKey(objectSymbol);
            if (state.ObjectIds.ContainsKey(objectKey))
            {
                continue;
            }

            var objectModel = CreateObjectModel(objectDeclaration, objectSymbol, fileId);
            var objectId = await _objectRepository.InsertOrUpdateAsync(objectModel);
            state.ObjectIds[objectKey] = objectId;

            CollectObjectRelationships(objectSymbol, objectKey, state);

            foreach (var memberDeclaration in GetMemberDeclarations(objectDeclaration))
            {
                foreach (var pendingMember in CreateMembers(memberDeclaration, semanticModel, objectId))
                {
                    if (state.MemberIds.ContainsKey(pendingMember.SymbolKey))
                    {
                        continue;
                    }

                    var memberId = await _memberRepository.InsertOrUpdateAsync(pendingMember.Model);
                    state.MemberIds[pendingMember.SymbolKey] = memberId;

                    CollectSignatureRelationships(objectKey, pendingMember.Symbol, pendingMember.SymbolKey, state);
                    CollectBodyRelationships(
                        objectKey,
                        pendingMember.SymbolKey,
                        memberDeclaration,
                        pendingMember.Model.IsTestMember,
                        semanticModel,
                        state);
                }
            }
        }
    }

    private async Task<int> EnsureFilePersistedAsync(SyntaxNode root, int analysisProjectId, string filePath)
    {
        var usingStatements = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(x => x.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var fileModel = new FileModel(usingStatements, analysisProjectId, filePath);
        return await _fileRepository.InsertOrUpdateAsync(fileModel);
    }

    private static IEnumerable<MemberDeclarationSyntax> GetMemberDeclarations(BaseTypeDeclarationSyntax objectDeclaration)
    {
        return objectDeclaration switch
        {
            TypeDeclarationSyntax typeDeclaration => typeDeclaration.Members.Where(IsSupportedMemberDeclaration),
            EnumDeclarationSyntax enumDeclaration => enumDeclaration.Members,
            _ => Enumerable.Empty<MemberDeclarationSyntax>()
        };
    }

    private static bool IsSupportedMemberDeclaration(MemberDeclarationSyntax declaration)
    {
        return declaration is MethodDeclarationSyntax
            or ConstructorDeclarationSyntax
            or DestructorDeclarationSyntax
            or PropertyDeclarationSyntax
            or IndexerDeclarationSyntax
            or FieldDeclarationSyntax
            or EventDeclarationSyntax
            or EventFieldDeclarationSyntax
            or OperatorDeclarationSyntax
            or ConversionOperatorDeclarationSyntax
            or EnumMemberDeclarationSyntax;
    }

    private ObjectModel CreateObjectModel(BaseTypeDeclarationSyntax declaration, INamedTypeSymbol symbol, int fileId)
    {
        var members = GetMemberDeclarations(declaration).ToList();
        var testFramework = members
            .Select(ResolveTestFramework)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

        return new ObjectModel(
            attributes: GetAttributeStrings(declaration.AttributeLists),
            modifiers: GetModifierStrings(declaration.Modifiers),
            location: CreateLocation(declaration),
            fileId: fileId,
            @namespace: symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            name: symbol.Name,
            kind: GetObjectKind(symbol),
            docString: symbol.GetDocumentationCommentXml() ?? string.Empty,
            fullString: declaration.ToFullString().Trim(),
            isTestObject: members.Any(IsTestMember),
            testFramework: testFramework);
    }

    private IEnumerable<PendingMember> CreateMembers(MemberDeclarationSyntax declaration, SemanticModel semanticModel, int objectId)
    {
        switch (declaration)
        {
            case FieldDeclarationSyntax fieldDeclaration:
            {
                foreach (var variable in fieldDeclaration.Declaration.Variables)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                    if (symbol == null)
                    {
                        continue;
                    }

                    yield return new PendingMember(
                        GetSymbolKey(symbol),
                        symbol,
                        CreateMemberModel(fieldDeclaration, symbol, objectId, variable.Identifier.Text, "field"));
                }

                yield break;
            }
            case EventFieldDeclarationSyntax eventFieldDeclaration:
            {
                foreach (var variable in eventFieldDeclaration.Declaration.Variables)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(variable) as IEventSymbol;
                    if (symbol == null)
                    {
                        continue;
                    }

                    yield return new PendingMember(
                        GetSymbolKey(symbol),
                        symbol,
                        CreateMemberModel(eventFieldDeclaration, symbol, objectId, variable.Identifier.Text, "event"));
                }

                yield break;
            }
            default:
            {
                var symbol = semanticModel.GetDeclaredSymbol(declaration);
                if (symbol is not ISymbol memberSymbol)
                {
                    yield break;
                }

                yield return new PendingMember(
                    GetSymbolKey(memberSymbol),
                    memberSymbol,
                    CreateMemberModel(declaration, memberSymbol, objectId, memberSymbol.Name, GetMemberKind(memberSymbol)));
                yield break;
            }
        }
    }

    private MemberModel CreateMemberModel(
        MemberDeclarationSyntax declaration,
        ISymbol symbol,
        int objectId,
        string name,
        string kind)
    {
        return new MemberModel(
            attributes: GetAttributeStrings(declaration.AttributeLists),
            modifiers: GetModifierStrings(GetModifiers(declaration)),
            testCategories: GetTestCategories(declaration),
            location: CreateLocation(declaration),
            objectEntityId: objectId,
            name: name,
            kind: kind,
            docString: symbol.GetDocumentationCommentXml() ?? string.Empty,
            fullString: declaration.ToFullString().Trim(),
            isTestMember: IsTestMember(declaration),
            testIntent: string.Empty,
            isGenerated: false,
            testMetadataSource: string.Empty,
            testMetadataConfidence: null,
            testMetadataPromptVersion: string.Empty);
    }

    private void CollectObjectRelationships(INamedTypeSymbol symbol, string objectKey, AnalysisState state)
    {
        if (symbol.ContainingType != null && state.IsProjectSymbol(symbol.ContainingType))
        {
            state.ObjectRelationships.Add(new PendingRelationship(objectKey, GetSymbolKey(symbol.ContainingType), "contained_by"));
        }

        if (symbol.BaseType != null &&
            symbol.BaseType.SpecialType != SpecialType.System_Object &&
            state.IsProjectSymbol(symbol.BaseType))
        {
            state.ObjectRelationships.Add(new PendingRelationship(objectKey, GetSymbolKey(symbol.BaseType), "inherits"));
        }

        foreach (var interfaceType in symbol.Interfaces.Where(state.IsProjectSymbol))
        {
            state.ObjectRelationships.Add(new PendingRelationship(objectKey, GetSymbolKey(interfaceType), "implements"));
        }
    }

    private void CollectSignatureRelationships(string objectKey, ISymbol memberSymbol, string memberKey, AnalysisState state)
    {
        foreach (var typeSymbol in GetReferencedTypes(memberSymbol).Where(state.IsProjectSymbol))
        {
            state.ObjectRelationships.Add(new PendingRelationship(objectKey, GetSymbolKey(typeSymbol), "uses"));
        }

        foreach (var relatedMember in GetReferencedMembersFromSignature(memberSymbol).Where(state.IsProjectSymbol))
        {
            state.MemberRelationships.Add(new PendingRelationship(memberKey, GetSymbolKey(relatedMember), "references"));
            if (relatedMember.ContainingType != null && state.IsProjectSymbol(relatedMember.ContainingType))
            {
                state.ObjectRelationships.Add(new PendingRelationship(objectKey, GetSymbolKey(relatedMember.ContainingType), "uses"));
            }
        }
    }

    private void CollectBodyRelationships(
        string objectKey,
        string memberKey,
        MemberDeclarationSyntax declaration,
        bool isTestMember,
        SemanticModel semanticModel,
        AnalysisState state)
    {
        foreach (var node in declaration.DescendantNodes())
        {
            if (node is not InvocationExpressionSyntax
                and not ObjectCreationExpressionSyntax
                and not IdentifierNameSyntax
                and not MemberAccessExpressionSyntax)
            {
                continue;
            }

            var symbol = ResolveReferencedSymbol(node, semanticModel);
            if (node is InvocationExpressionSyntax assertionInvocation &&
                isTestMember &&
                (symbol == null || !state.IsProjectSymbol(symbol)) &&
                IsAssertionInvocation(assertionInvocation, symbol))
            {
                state.Invocations.Add(new PendingInvocation(
                    memberKey,
                    null,
                    node.ToFullString().Trim(),
                    CreateLocation(node),
                    true));
                continue;
            }

            if (symbol == null || !state.IsProjectSymbol(symbol))
            {
                continue;
            }

            var relationshipType = GetMemberRelationshipType(node, symbol);
            if (relationshipType != null)
            {
                var targetMemberKey = GetSymbolKey(symbol);
                state.MemberRelationships.Add(new PendingRelationship(memberKey, targetMemberKey, relationshipType));

                if (node is InvocationExpressionSyntax invocationExpression)
                {
                    state.Invocations.Add(new PendingInvocation(
                        memberKey,
                        targetMemberKey,
                        node.ToFullString().Trim(),
                        CreateLocation(node),
                        isTestMember && IsAssertionInvocation(invocationExpression, symbol)));
                }
            }

            if (symbol.ContainingType != null && state.IsProjectSymbol(symbol.ContainingType))
            {
                state.ObjectRelationships.Add(new PendingRelationship(objectKey, GetSymbolKey(symbol.ContainingType), "uses"));
            }
        }
    }

    private async Task PersistRelationshipsAsync(AnalysisState state)
    {
        foreach (var relationship in state.ObjectRelationships)
        {
            if (!state.ObjectIds.TryGetValue(relationship.SourceKey, out var sourceId) ||
                !state.ObjectIds.TryGetValue(relationship.TargetKey, out var targetId) ||
                sourceId == targetId && relationship.RelationshipType == "uses")
            {
                continue;
            }

            await _objectRelationshipRepository.InsertOrUpdateAsync(
                new ObjectRelationshipModel(sourceId, targetId, relationship.RelationshipType));
        }

        foreach (var relationship in state.MemberRelationships)
        {
            if (!state.MemberIds.TryGetValue(relationship.SourceKey, out var sourceId) ||
                !state.MemberIds.TryGetValue(relationship.TargetKey, out var targetId) ||
                sourceId == targetId)
            {
                continue;
            }

            await _memberRelationshipRepository.InsertOrUpdateAsync(
                new MemberRelationshipModel(sourceId, targetId, relationship.RelationshipType));
        }

        foreach (var invocation in state.Invocations)
        {
            if (!state.MemberIds.TryGetValue(invocation.SourceKey, out var memberId))
            {
                continue;
            }

            int? invokedMemberId = null;
            if (invocation.TargetKey != null)
            {
                if (!state.MemberIds.TryGetValue(invocation.TargetKey, out var resolvedInvokedMemberId))
                {
                    continue;
                }

                invokedMemberId = resolvedInvokedMemberId;
            }

            await _invocationRepository.InsertOrUpdateAsync(
                new InvocationModel(
                    location: invocation.Location,
                    memberId: memberId,
                    invokedMemberId: invokedMemberId,
                    isAssertion: invocation.IsAssertion,
                    fullString: invocation.FullString));
        }
    }

    private string ResolveTestFramework(MemberDeclarationSyntax declaration)
    {
        foreach (var framework in _context.Project.Config.RuntimeConfig.Frameworks ?? new Dictionary<string, List<string>>())
        {
            if (declaration.AttributeLists
                .SelectMany(x => x.Attributes)
                .Any(attribute => framework.Value.Contains(attribute.Name.ToString())))
            {
                return framework.Key;
            }
        }

        return string.Empty;
    }

    private bool IsTestMember(MemberDeclarationSyntax declaration)
    {
        return !string.IsNullOrWhiteSpace(ResolveTestFramework(declaration));
    }

    private static List<string> GetTestCategories(MemberDeclarationSyntax declaration)
    {
        return declaration.AttributeLists
            .SelectMany(x => x.Attributes)
            .Select(x => x.Name.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<ITypeSymbol> GetReferencedTypes(ISymbol symbol)
    {
        var types = new List<ITypeSymbol>();

        switch (symbol)
        {
            case IMethodSymbol methodSymbol:
                AddType(types, methodSymbol.ReturnType);
                foreach (var parameter in methodSymbol.Parameters)
                {
                    AddType(types, parameter.Type);
                }

                break;
            case IPropertySymbol propertySymbol:
                AddType(types, propertySymbol.Type);
                foreach (var parameter in propertySymbol.Parameters)
                {
                    AddType(types, parameter.Type);
                }

                break;
            case IFieldSymbol fieldSymbol:
                AddType(types, fieldSymbol.Type);
                break;
            case IEventSymbol eventSymbol:
                AddType(types, eventSymbol.Type);
                break;
        }

        return types;
    }

    private static IEnumerable<ISymbol> GetReferencedMembersFromSignature(ISymbol symbol)
    {
        if (symbol is IPropertySymbol propertySymbol)
        {
            if (propertySymbol.GetMethod != null)
            {
                yield return propertySymbol.GetMethod;
            }

            if (propertySymbol.SetMethod != null)
            {
                yield return propertySymbol.SetMethod;
            }
        }
    }

    private static void AddType(List<ITypeSymbol> types, ITypeSymbol? typeSymbol)
    {
        if (typeSymbol == null)
        {
            return;
        }

        types.Add(typeSymbol);

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            foreach (var argument in namedType.TypeArguments.OfType<ITypeSymbol>())
            {
                types.Add(argument);
            }
        }
    }

    private static ISymbol? ResolveReferencedSymbol(SyntaxNode node, SemanticModel semanticModel)
    {
        var info = semanticModel.GetSymbolInfo(node);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();

        return symbol switch
        {
            IMethodSymbol methodSymbol => methodSymbol.OriginalDefinition,
            IPropertySymbol propertySymbol => propertySymbol,
            IFieldSymbol fieldSymbol => fieldSymbol,
            IEventSymbol eventSymbol => eventSymbol,
            _ => null
        };
    }

    private static string? GetMemberRelationshipType(SyntaxNode node, ISymbol symbol)
    {
        return node switch
        {
            InvocationExpressionSyntax when symbol is IMethodSymbol => "calls",
            ObjectCreationExpressionSyntax when symbol is IMethodSymbol => "creates",
            IdentifierNameSyntax when symbol is IFieldSymbol or IPropertySymbol or IEventSymbol => "references",
            MemberAccessExpressionSyntax when symbol is IFieldSymbol or IPropertySymbol or IEventSymbol => "references",
            _ => null
        };
    }

    private static bool IsAssertionInvocation(InvocationExpressionSyntax invocation, ISymbol? symbol)
    {
        var methodSymbol = symbol as IMethodSymbol;
        var containingTypeName = methodSymbol?.ContainingType?.Name ?? string.Empty;
        var containingNamespace = methodSymbol?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        var methodName = methodSymbol?.Name ?? ExtractInvocationMethodName(invocation);

        if (containingTypeName.Contains("Assert", StringComparison.OrdinalIgnoreCase) ||
            containingTypeName.Contains("Assertion", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (containingNamespace.Contains("FluentAssertions", StringComparison.OrdinalIgnoreCase) ||
            containingNamespace.Contains("Shouldly", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return methodName switch
        {
            "True" or
            "False" or
            "Equal" or
            "NotEqual" or
            "Same" or
            "NotSame" or
            "Null" or
            "NotNull" or
            "Empty" or
            "NotEmpty" or
            "Contains" or
            "DoesNotContain" or
            "StartsWith" or
            "EndsWith" or
            "Matches" or
            "Throws" or
            "ThrowsAsync" or
            "Throw" or
            "ThrowAsync" or
            "Fail" or
            "That" or
            "ShouldBe" or
            "ShouldNotBe" or
            "ShouldContain" or
            "ShouldNotContain" or
            "Be" or
            "BeTrue" or
            "BeFalse" or
            "BeNull" or
            "NotBeNull" or
            "BeEquivalentTo" or
            "ContainSingle" => true,
            _ => invocation.ToFullString().Contains("Assert.", StringComparison.Ordinal)
        };
    }

    private static string ExtractInvocationMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            MemberAccessExpressionSyntax memberAccessExpression => memberAccessExpression.Name.Identifier.Text,
            _ => string.Empty
        };
    }

    private static string GetObjectKind(INamedTypeSymbol symbol)
    {
        if (symbol.IsRecord)
        {
            return symbol.TypeKind == TypeKind.Struct ? "record_struct" : "record";
        }

        return symbol.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => symbol.TypeKind.ToString().ToLowerInvariant()
        };
    }

    private static string GetMemberKind(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol methodSymbol => methodSymbol.MethodKind switch
            {
                MethodKind.Constructor => "constructor",
                MethodKind.StaticConstructor => "static_constructor",
                MethodKind.Destructor => "destructor",
                MethodKind.PropertyGet => "property_getter",
                MethodKind.PropertySet => "property_setter",
                MethodKind.EventAdd => "event_adder",
                MethodKind.EventRemove => "event_remover",
                MethodKind.UserDefinedOperator => "operator",
                MethodKind.Conversion => "conversion_operator",
                _ => "method"
            },
            IPropertySymbol => "property",
            IFieldSymbol => "field",
            IEventSymbol => "event",
            _ => symbol.Kind.ToString().ToLowerInvariant()
        };
    }

    private static SyntaxTokenList GetModifiers(MemberDeclarationSyntax declaration)
    {
        return declaration switch
        {
            BaseTypeDeclarationSyntax baseTypeDeclaration => baseTypeDeclaration.Modifiers,
            BaseMethodDeclarationSyntax baseMethodDeclaration => baseMethodDeclaration.Modifiers,
            BasePropertyDeclarationSyntax basePropertyDeclaration => basePropertyDeclaration.Modifiers,
            BaseFieldDeclarationSyntax baseFieldDeclaration => baseFieldDeclaration.Modifiers,
            EnumMemberDeclarationSyntax => default,
            _ => default
        };
    }

    private static List<string> GetModifierStrings(SyntaxTokenList modifiers)
    {
        return modifiers.Select(x => x.Text).ToList();
    }

    private static List<string> GetAttributeStrings(SyntaxList<AttributeListSyntax> attributeLists)
    {
        return attributeLists.Select(x => x.ToString()).ToList();
    }

    private static CodeLocation CreateLocation(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return new CodeLocation(
            span.StartLinePosition.Line,
            span.StartLinePosition.Character,
            span.EndLinePosition.Line,
            span.EndLinePosition.Character);
    }

    private static string GetSymbolKey(ISymbol symbol)
    {
        return symbol.GetDocumentationCommentId()
               ?? $"{symbol.Kind}:{symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";
    }

    private static bool ShouldAnalyzeDocument(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
               && !filePath.EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)
               && !filePath.EndsWith("AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AnalysisState
    {
        private readonly HashSet<string> _projectDocumentPaths;

        public AnalysisState(HashSet<string> projectDocumentPaths)
        {
            _projectDocumentPaths = projectDocumentPaths;
        }

        public Dictionary<string, int> ObjectIds { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> MemberIds { get; } = new(StringComparer.Ordinal);
        public HashSet<PendingRelationship> ObjectRelationships { get; } = new();
        public HashSet<PendingRelationship> MemberRelationships { get; } = new();
        public HashSet<PendingInvocation> Invocations { get; } = new();

        public bool IsProjectSymbol(ISymbol symbol)
        {
            return symbol.Locations.Any(location =>
                location.IsInSource &&
                location.SourceTree?.FilePath != null &&
                _projectDocumentPaths.Contains(location.SourceTree.FilePath));
        }
    }

    private sealed record PendingMember(string SymbolKey, ISymbol Symbol, MemberModel Model);
    private sealed record PendingRelationship(string SourceKey, string TargetKey, string RelationshipType);
    private sealed record PendingInvocation(
        string SourceKey,
        string? TargetKey,
        string FullString,
        CodeLocation Location,
        bool IsAssertion);
}
