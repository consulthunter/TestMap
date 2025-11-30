using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models;
using TestMap.Models.Database;
using TestMap.Services.Database;

namespace TestMap.Services.CollectInformation;

public class MapUnresolvedService : IMapUnresolvedService
{
    private ProjectModel _projectModel;
    private SqliteDatabaseService _sqliteDatabaseService;

    public MapUnresolvedService(ProjectModel projectModel, SqliteDatabaseService sqliteDatabaseService)
    {
        _projectModel = projectModel;
        _sqliteDatabaseService = sqliteDatabaseService;
    }

    public async Task MapUnresolvedAsync()
    {
        await MapUnresolvedInvocations();
    }

    private async Task MapUnresolvedInvocations()
    {
        var invocationDetails = await _sqliteDatabaseService.InvocationRepository.GetUnresolvedInvocations();

        foreach (var invocationDetail in invocationDetails)
        {
            var analysisProject = _projectModel.Projects.FirstOrDefault(x => x.Guid == invocationDetail.ProjectGuid);
            if (analysisProject != null)
            {
                var compilation = analysisProject.Compilation;
                if (compilation != null)
                {
                    var syntaxTrees = compilation.SyntaxTrees;
                    var document = syntaxTrees.Where(x => x.FilePath == invocationDetail.FilePath).FirstOrDefault();
                    if (document != null)
                    {
                        var root = await document.GetRootAsync();
                        var semanticModel = compilation.GetSemanticModel(document);
                        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Where(x => x.ToFullString().Contains(invocationDetail.FullString));

                        var invocationExpressionSyntaxes = invocation.ToList();
                        if (invocationExpressionSyntaxes.Any())
                            foreach (var invocationExpression in invocationExpressionSyntaxes)
                            {
                                // use the semantic model to do symbol resolving
                                // to find the definition for the method being invoked
                                var info = semanticModel.GetSymbolInfo(invocationExpression);
                                var methodSymbol = info.Symbol;

                                // the symbol could be null
                                // if the symbol is not defined in syntax trees that
                                // are loaded in the csharp compilation
                                if (methodSymbol != null)
                                {
                                    // what needs to happen here,
                                    // if not null we need to keep a list of these items and return
                                    var declaration = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                                    if (declaration != null)
                                    {
                                        var definition = await declaration.GetSyntaxAsync() as MethodDeclarationSyntax;
                                        if (definition != null)
                                        {
                                            var name = definition?.Identifier.ToFullString().Trim();
                                            var definitionFilePath = definition?.SyntaxTree.FilePath;

                                            if (name != null && definitionFilePath != null)
                                            {
                                                var sourceMethodId =
                                                    await _sqliteDatabaseService.MethodRepository.FindMethod(name,
                                                        definitionFilePath);

                                                if (sourceMethodId != 0)
                                                    await _sqliteDatabaseService.InvocationRepository
                                                        .UpdateInvocationSourceId(invocationDetail.InvocationId,
                                                            sourceMethodId);
                                                else
                                                    _projectModel.Logger?.Warning(
                                                        $"Matching Invocation Not Found {name} {definitionFilePath}");
                                            }

                                            _projectModel.Logger?.Information($"Declaration found: {definition}");
                                        }
                                        else
                                        {
                                            _projectModel.Logger?.Warning(
                                                $"Definition not found. Declaration found: {declaration}");
                                        }
                                    }
                                    else
                                    {
                                        _projectModel.Logger?.Warning(
                                            $"Declaration Not Found. Method symbol {methodSymbol}");
                                    }
                                }
                                else
                                {
                                    _projectModel.Logger?.Warning(
                                        $"Method symbol not found: {invocationDetail.FullString}");
                                }
                            }
                        else
                            _projectModel.Logger?.Warning($"Not found: {invocationDetail.FullString}");
                    }
                    else
                    {
                        _projectModel.Logger?.Warning(
                            $"Could not find syntax tree with name: {invocationDetail.FullString}");
                    }
                }
                else
                {
                    _projectModel.Logger?.Warning($"No compilation found for {invocationDetail.FullString}");
                }
            }
            else
            {
                _projectModel.Logger?.Warning($"No such project: {invocationDetail.ProjectGuid}");
            }
        }
    }

    private async Task MapLocalImports()
    {
    }
}