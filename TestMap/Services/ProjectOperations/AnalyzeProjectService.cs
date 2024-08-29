using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class AnalyzeProjectService
{
    private ProjectModel _projectModel;
    private string MethodOutFile;
    private string ClassOutFile;
    private readonly Dictionary<string, string> _attributeNames = new()
    {
        { "Fact", "xUnit" },
        { "Test", "NUnit" },
        { "TestMethod", "MSTest" },
    };
    public AnalyzeProjectService(ProjectModel projectModel)
    {
        try
        {
            _projectModel = projectModel;
            MethodOutFile = Path.Combine(_projectModel.OutputPath,
                $"test_methods_{_projectModel.ProjectId}.csv");
            ClassOutFile = Path.Combine(_projectModel.OutputPath,
                $"test_classes_{_projectModel.ProjectId}.csv");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    public virtual async Task AnalyzeProjectAsync(AnalysisProject analysisProject, CSharpCompilation cSharpCompilation)
    {
        _projectModel.Logger.Information($"Analyzing project {analysisProject.ProjectFilePath}");
        // for every .cs file in the current project
        foreach (var document in analysisProject.Documents)
        {
            _projectModel.Logger.Information($"Analyzing {document}");
            SyntaxTree syntaxTree = analysisProject.SyntaxTrees[document];
            CSharpCompilation compilation = cSharpCompilation;
            
            // Necessary to analyze types and retrieve declarations
            // for invocations
            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
            
            var root = await syntaxTree.GetRootAsync();

            var namespaceDec = FindNamespace(root);

            var usings = GetUsingStatements(root);
            
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            var classDeclarationSyntaxes = classDeclarations.ToList();
            
            _projectModel.Logger.Information($"Number of class declarations: {classDeclarationSyntaxes.Count}");
            foreach (var classDeclaration in classDeclarationSyntaxes)
            {
                _projectModel.Logger.Information($"Class declaration: {classDeclaration.Identifier.ToString()}");
                var fieldDeclarations = FindFields(classDeclaration).Select(x => x.ToString()).ToList();
                var methodDeclarations = FindMethods(classDeclaration);
                
                _projectModel.Logger.Information($"Number of field declarations: {fieldDeclarations.Count}");
                _projectModel.Logger.Information($"Number of method declarations: {methodDeclarations.Count}");
                // if there is a test
                // then this is a test class
                if (methodDeclarations.Any())
                {
                    // look for the source code class
                    string trimmedDocPath = document.Split("\\").Last().Replace("Test", "").Replace("test", "")
                        .Replace("Tests", "").Replace("tests", "");
                    var sourceClass =
                        compilation.SyntaxTrees.Where(x => x.FilePath.Split("\\").Last() == trimmedDocPath).ToList();

                    // if we found it write
                    if (sourceClass.Any())
                    {
                        TestClassRecord testClassRecord = new TestClassRecord
                        (
                            _projectModel.RepoName,
                            document,
                            namespaceDec,
                            classDeclaration.Identifier.ToString(),
                            fieldDeclarations,
                            usings,
                            methodDeclarations.First().Item2,
                            classDeclaration.ToFullString().Trim(),
                            sourceClass.First().ToString()
                        );
                        WriteResults(testClassRecord);
                    }
                    else
                    {
                        _projectModel.Logger.Warning($"No source code class found for {document} test class.");
                    }
                    foreach (var method in methodDeclarations)
                    {
                        var methodInvocations = FindInvocations(method.Item1, semanticModel);

                        _projectModel.Logger.Information($"Method {method.Item1.Identifier.ToString()}");
                        _projectModel.Logger.Information($"Number of invocations: {methodInvocations.Count}");

                        // if any 
                        if (methodInvocations.Any())
                        {
                            
                            TestMethodRecord testMethodRecord = new TestMethodRecord
                            (
                                _projectModel.RepoName,
                                document,
                                namespaceDec,
                                classDeclaration.Identifier.ToString(),
                                fieldDeclarations,
                                usings,
                                method.Item2,
                                method.Item1.ToString(),
                                methodInvocations
                            );

                            WriteResults(testMethodRecord);
                        }
                    }
                }
            }
            _projectModel.Logger.Information($"Finished analyzing {document}");
        }
    }

    private string FindNamespace(SyntaxNode syntaxNode)
    {
        _projectModel.Logger.Information($"Looking for namespace.");
        // Namespace typically comes in two forms
        // namespace XXX; (NamespaceDeclarationSyntax)
        // And
        // namspace XXX {
        // ...
        // } (FilescopedNamespaceDeclaration)
        // They are represented as different SyntaxNodes
        // Hence the if/else
        string namespaceDec = "";
        var namespaceDeclaration = syntaxNode.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                    
        // try the first syntax node
        if (namespaceDeclaration != null)
        {
            namespaceDec = namespaceDeclaration.Name.ToFullString();
        }
        // try the second syntax node
        else
        {
            var fileScopedNamespaceDeclarationDeclaration = syntaxNode.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>()
                .FirstOrDefault();

            if (fileScopedNamespaceDeclarationDeclaration != null)
            {
                namespaceDec = fileScopedNamespaceDeclarationDeclaration.Name.ToFullString();
            }
            // if it's not either of them, then the namespace may not be present in the file.
            else
            { 
                _projectModel.Logger.Information("No namespace found.");
            }
        }
        _projectModel.Logger.Information("Finished looking for namespace.");
        return namespaceDec;
    }

    private List<string> GetUsingStatements(SyntaxNode syntaxNode)
    {
        _projectModel.Logger.Information("Looking for using statements.");
        List<string> usingStatements = new List<string>();
        
        // Get all using directives
        var usingDirectives = syntaxNode.DescendantNodes().OfType<UsingDirectiveSyntax>();
        foreach (var usingDirective in usingDirectives)
        {
            if (usingDirective.Name != null) usingStatements.Add(usingDirective.Name.ToFullString());
        }
        _projectModel.Logger.Information("Finished looking for using statements.");
        return usingStatements;
    }
    
    private List<FieldDeclarationSyntax> FindFields(SyntaxNode syntaxNode)
    {
        return syntaxNode.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
    }

    private List<(MethodDeclarationSyntax, string)> FindMethods(SyntaxNode syntaxNode)
    {
        _projectModel.Logger.Information($"Looking for test method declarations.");
        var methods = syntaxNode.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        List<(MethodDeclarationSyntax, string)> testMethods = new();
        foreach (var method in methods)
        {
            (string name, bool result, string framework) = IsTestMethod(method);
            if (result)
            {
                testMethods.Add((method, framework));
            }
        }
        _projectModel.Logger.Information($"Looking for test method declarations.");
        return testMethods;
    }

    private (string, bool, string) IsTestMethod(MethodDeclarationSyntax methodDeclarationSyntax)
    {
        var result = methodDeclarationSyntax.AttributeLists.SelectMany(al => al.Attributes)
            .Select(attr =>
            {
                string attributeName = attr.Name.ToString();
                bool exists = _attributeNames.ContainsKey(attributeName);
                string framework = (exists ? _attributeNames[attributeName] : null) ?? string.Empty;
                return (attributeName, exists, framework);
            })
            .FirstOrDefault();
        return result;
    }

    private List<(string, string)> FindInvocations(MethodDeclarationSyntax methodDeclarationSyntax,
        SemanticModel semanticModel)
    {
        _projectModel.Logger.Information($"Looking for method invocations.");
        List<(string, string)> invocationDeclarations = new();
        var invocations = methodDeclarationSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var info = semanticModel.GetSymbolInfo(invocation);
            var methodSymbol = info.Symbol;
            if (methodSymbol != null)
            {
                string declaration = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().ToString() ??
                                     string.Empty;
                invocationDeclarations.Add((invocation.ToString(), declaration));
            }
            else
            {
                invocationDeclarations.Add((invocation.ToString(), String.Empty));
            }
        }
        _projectModel.Logger.Information($"Finished looking for method invocations.");
        
        return invocationDeclarations;
    }

    private void WriteResults(TestMethodRecord testMethodRecord)
    {
        // Prepare header string
        string header = "Repo,FilePath,Namespace,ClassDeclaration,ClassFields,UsingStatements,Framework,MethodBody,MethodInvocations";

        string usings = ReplaceNewlines(string.Join(";", testMethodRecord.UsingStatements));
        string methodInvocations = ReplaceNewlines(string.Join(";", testMethodRecord.MethodInvocations));
        string fields = ReplaceNewlines(string.Join(";", testMethodRecord.ClassFields));

        string csvLine = $"\'{testMethodRecord.Repo}\',\'{testMethodRecord.FilePath}\',\'{testMethodRecord.Namespace}\',\'{testMethodRecord.ClassDeclaration}\',\'{fields}\',\'{usings}\',\'{testMethodRecord.Framework}\',\'{ReplaceNewlines(testMethodRecord.MethodBody)}\',\'{methodInvocations}\'";
        

        // Write header to CSV file
        if (!File.Exists(MethodOutFile))
        {
            using (StreamWriter writer = new StreamWriter(MethodOutFile, true))
            {
                writer.WriteLine(header);
            }
        }

        // Write data row to CSV file
        using (StreamWriter writer = new StreamWriter(MethodOutFile, true))
        {
            writer.WriteLine(csvLine);
        }
    }
    private void WriteResults(TestClassRecord testClassRecord)
    {
        // Prepare header string
        string header = "Repo,FilePath,Namespace,ClassDeclaration,ClassFields,UsingStatements,Framework,ClassBody,SourceBody";

        string usings = ReplaceNewlines(string.Join(";", testClassRecord.UsingStatements));
        string fields = ReplaceNewlines(string.Join(";", testClassRecord.ClassFields));

        string csvLine = $"\'{testClassRecord.Repo}\',\'{testClassRecord.FilePath}\',\'{testClassRecord.Namespace}\',\'{testClassRecord.ClassDeclaration}\',\'{fields}\',\'{usings}\',\'{testClassRecord.Framework}\',\'{ReplaceNewlines(testClassRecord.ClassBody)}\',\'{ReplaceNewlines(testClassRecord.SourceBody)}\'";
        

        // Write header to CSV file
        if (!File.Exists(ClassOutFile))
        {
            using (StreamWriter writer = new StreamWriter(ClassOutFile, true))
            {
                writer.WriteLine(header);
            }
        }

        // Write data row to CSV file
        using (StreamWriter writer = new StreamWriter(ClassOutFile, true))
        {
            writer.WriteLine(csvLine);
        }
    }
    private string ReplaceNewlines(string str)
    {
        return str.Replace("\r\n", "<<NEWLINE>>")
            .Replace("\r", "<<NEWLINE>>")
            .Replace("\n", "<<NEWLINE>>")
            .Replace(Environment.NewLine, "<<NEWLINE>>")
            .Replace("'", "\\'");
    }
}