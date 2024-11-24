/*
 * consulthunter
 * 2024-11-07
 * Looks at the syntaxTrees
 * for test methods and test classes
 *
 * Test methods are collected as well
 * as methods used within the test and
 * their definitions
 *
 * Test classes are collected by using filepaths
 * and project references
 *
 * We look at syntaxTrees one-by-one and
 * append the records to the CSvs
 * AnalyzeProjectService.cs
 */

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class AnalyzeProjectService : IAnalyzeProjectService
{
    private readonly ProjectModel _projectModel;
    private readonly string _methodOutFile;
    private readonly string _classOutFile;

    public AnalyzeProjectService(ProjectModel projectModel)
    {
        try
        {
            _projectModel = projectModel;
            _methodOutFile = Path.Combine(_projectModel.OutputPath,
                $"test_methods_{_projectModel.ProjectId}.csv");
            _classOutFile = Path.Combine(_projectModel.OutputPath,
                $"test_classes_{_projectModel.ProjectId}.csv");
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to load project model: {e.Message}");
        }
    }

    /// <summary>
    /// Uses the compilation to create the semantic model
    /// And gathers the necessary information to create
    /// the test method and test class records
    /// </summary>
    /// <param name="analysisProject">Analysis project, project we are analyzing</param>
    /// <param name="cSharpCompilation">Csharp compilation for the project</param>
    public virtual async Task AnalyzeProjectAsync(AnalysisProject analysisProject, CSharpCompilation? cSharpCompilation)
    {
        _projectModel.Logger.Information($"Analyzing project {analysisProject.ProjectFilePath}");
        // for every .cs file in the current project
        foreach (var document in cSharpCompilation.SyntaxTrees)
        {
            _projectModel.Logger.Information($"Analyzing {document.FilePath}");
            var compilation = cSharpCompilation;

            // Necessary to analyze types and retrieve declarations
            // for invocations
            var semanticModel = compilation.GetSemanticModel(document);

            var root = await document.GetRootAsync();

            var namespaceDec = FindNamespace(root);

            var usings = GetUsingStatements(root);

            var testingFramework = FindTestingFrameworkFromUsings(usings);

            var classDeclarationSyntaxes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();

            _projectModel.Logger.Information($"Number of class declarations: {classDeclarationSyntaxes.Count}");
            foreach (var classDeclaration in classDeclarationSyntaxes)
            {
                _projectModel.Logger.Information($"Class declaration: {classDeclaration.Identifier.ToString()}");
                var fieldDeclarations = FindFields(classDeclaration);
                var methodDeclarations = FindMethods(classDeclaration, testingFramework);
                
                // if there is a test
                // then this is a test class
                if (methodDeclarations.Any())
                {
                    // try to find the source code (class being tested) using
                    // project references and filepaths
                    SyntaxTree? sourceClass = FindSourceClass(analysisProject, document);
                    
                    if (sourceClass != null)
                    {
                        TestClassRecord testClassRecord = CreateTestClassRecord(analysisProject, document, namespaceDec,
                            classDeclaration, fieldDeclarations, usings, testingFramework, sourceClass);
                        WriteResults(testClassRecord);
                    }
                    else
                    {
                        _projectModel.Logger.Warning($"No source code class found for {document.FilePath} test class.");
                    }

                    foreach (var method in methodDeclarations)
                    {
                        // look for method invocations and their definitions
                        // within the test method
                        var methodInvocations = FindInvocations(method.Item1, semanticModel);

                        _projectModel.Logger.Information($"Method {method.Item1.Identifier.ToString()}");
                        _projectModel.Logger.Information($"Number of invocations: {methodInvocations.Count}");

                        // if we have any method invocations 
                        // then we can create the record
                        if (methodInvocations.Any())
                        {
                            TestMethodRecord testMethodRecord = CreateTestMethodRecord(analysisProject, document,
                                namespaceDec, classDeclaration, fieldDeclarations, usings, method, methodInvocations);
                            WriteResults(testMethodRecord);
                        }
                    }
                }
            }

            _projectModel.Logger.Information($"Finished analyzing {document.FilePath}");
        }
    }

    /// <summary>
    /// Looks for the namespace defined in the document
    /// using the root node
    /// </summary>
    /// <param name="rootNode">Root node of the document</param>
    /// <returns>String, namespace identifier if found</returns>
    private string FindNamespace(SyntaxNode rootNode)
    {
        _projectModel.Logger.Information($"Looking for namespace.");
        var namespaceDec = "";
        var namespaceDeclaration = rootNode.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

        // try the first syntax node
        if (namespaceDeclaration != null)
        {
            namespaceDec = namespaceDeclaration.Name.ToFullString();
        }
        // try the second syntax node
        else
        {
            var fileScopedNamespaceDeclarationDeclaration = rootNode.DescendantNodes()
                .OfType<FileScopedNamespaceDeclarationSyntax>()
                .FirstOrDefault();

            if (fileScopedNamespaceDeclarationDeclaration != null)
                namespaceDec = fileScopedNamespaceDeclarationDeclaration.Name.ToFullString();
            // if it's not either of them, then the namespace may not be present in the file.
            else
                _projectModel.Logger.Warning("No namespace found.");
        }

        _projectModel.Logger.Information("Finished looking for namespace.");
        return namespaceDec;
    }

    /// <summary>
    /// Looks for usings statements from the root node of the document
    /// </summary>
    /// <param name="rootNode">Root node of the document</param>
    /// <returns>List of strings, using statements</returns>
    private List<string> GetUsingStatements(SyntaxNode rootNode)
    {
        _projectModel.Logger.Information("Looking for using statements.");
        List<string> usingStatements = new();

        // Get all using directives
        var usingDirectives = rootNode.DescendantNodes().OfType<UsingDirectiveSyntax>();
        foreach (var usingDirective in usingDirectives)
            if (usingDirective.Name != null)
                usingStatements.Add(usingDirective.Name.ToFullString());
        _projectModel.Logger.Information($"Number of using statements found. {usingStatements.Count}");
        _projectModel.Logger.Information("Finished looking for using statements.");
        return usingStatements;
    }

    /// <summary>
    /// Searches the using statements for a testing framework
    /// that is defined within the config
    /// </summary>
    /// <param name="usings">List of usings statements</param>
    /// <returns>Testing framework that matches from the config if present</returns>
    private string FindTestingFrameworkFromUsings(List<string> usings)
    {
        string testingFramework = "";
        _projectModel.Logger.Information("Looking for testing framework.");
        foreach (var usingStatement in usings)
        {
            foreach (var framework in _projectModel.TestingFrameworks.Keys)
            {
                if (usingStatement.ToLower().Contains(framework.ToLower()))
                {
                    testingFramework = framework;
                }
            }
        }

        if (string.IsNullOrEmpty(testingFramework))
        {
            _projectModel.Logger.Warning("No testing framework found.");
        }

        _projectModel.Logger.Information("Finished looking for testing framework.");
        return testingFramework;
    }

    /// <summary>
    /// Finds the corresponding source code class (class being tested)
    /// That matches with the current test code class
    /// </summary>
    /// <param name="analysisProject">Analysis project</param>
    /// <param name="document">Current document (i.e test code class)</param>
    /// <returns>SyntaxTree if we found a match</returns>
    private SyntaxTree? FindSourceClass(AnalysisProject analysisProject, SyntaxTree document)
    {
        // look through project model for referenced project
        // look in referenced project syntax trees
        List<AnalysisProject> referencedProjects = new();
        foreach (var reference in analysisProject.ProjectReferences)
        {
            referencedProjects.AddRange(
                _projectModel.Projects.Where(x => x.ProjectFilePath.Equals(reference)));
        }

        // Looking for cases of:
        // Project (Project A) has a reference to another project (Project B) where
        // Project A has a syntax tree of .*.cs such as Student.cs
        // Project B has a syntax tree of .*(Test|test|Tests|tests).cs such as StudentTest.cs
        // This should be exact matches minus the (Test|test|Tests|tests)
        // Assumption: Filepaths will be exact once the keyword is removed
        var trimmedDocPath = document.FilePath.Split("\\").Last().Replace("Test", "").Replace("test", "")
            .Replace("Tests", "").Replace("tests", "");

        SyntaxTree? sourceClass = null;

        foreach (var referencedProject in referencedProjects)
        {
            var path = referencedProject.SyntaxTrees.Keys.FirstOrDefault(x => x.Contains(trimmedDocPath));
            if (path != null) sourceClass = referencedProject.SyntaxTrees[path];
        }

        return sourceClass;
    }

    /// <summary>
    /// Looks for fields in the class
    /// </summary>
    /// <param name="classNode">Class node</param>
    /// <returns>List of strings, fields and properties defined in the class</returns>
    private List<string> FindFields(SyntaxNode classNode)
    {
        List<string> results = new List<string>();
        _projectModel.Logger.Information("Looking for fields.");
        results.AddRange(classNode.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList()
            .Select(x => x.ToString()));
        results.AddRange(classNode.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList()
            .Select(x => x.ToString()));
        _projectModel.Logger.Information($"Number of field declarations: {results.Count}.");
        _projectModel.Logger.Information("Finished looking for fields.");
        return results;
    }

    /// <summary>
    /// Looks for method declaration syntax in the document
    /// </summary>
    /// <param name="classNode">Class node</param>
    /// <param name="testingFramework">Test framework found in the usings</param>
    /// <returns>List of tuples, (method, test framework)</returns>
    private List<(MethodDeclarationSyntax, string)> FindMethods(SyntaxNode classNode, string testingFramework)
    {
        _projectModel.Logger.Information($"Looking for test method declarations.");
        var methods = classNode.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        List<(MethodDeclarationSyntax, string)> testMethods = new();
        foreach (var method in methods)
        {
            (var name, var result, var framework) = IsTestMethod(method, testingFramework);
            if (result) testMethods.Add((method, framework));
        }

        _projectModel.Logger.Information($"Finished looking for test method declarations.");
        return testMethods;
    }

    /// <summary>
    /// Checks to see if the method is a test method
    /// using the attributes defined within the config file
    /// </summary>
    /// <param name="methodDeclarationSyntax">Method defined in the document</param>
    /// <param name="testingFramework">Test framework found in the usings</param>
    /// <returns>Tuple, (attributeName, boolean (if the attribute is in the defined list), testingFramework)</returns>
    private (string, bool, string) IsTestMethod(MethodDeclarationSyntax methodDeclarationSyntax,
        string testingFramework)
    {
        // Get the list of attributes for the specified framework
        if (!_projectModel.TestingFrameworks.TryGetValue(testingFramework, out var frameworkAttributes))
        {
            // Return default if the framework is not found
            return (string.Empty, false, string.Empty);
        }

        // Check if the method has any of the attributes for the framework
        var result = methodDeclarationSyntax.AttributeLists
            .SelectMany(al => al.Attributes)
            .Select(attr =>
            {
                var attributeName = attr.Name.ToString();
                var exists = frameworkAttributes.Contains(attributeName);
                return (attributeName, exists, exists ? testingFramework : string.Empty);
            })
            .FirstOrDefault();

        return result;
    }

    /// <summary>
    /// Looks for method invocations within the test method
    /// </summary>
    /// <param name="methodDeclarationSyntax">Test method</param>
    /// <param name="semanticModel">Semantic model</param>
    /// <returns>List of tuples, (method invocation, method definition)</returns>
    private List<(string, string)> FindInvocations(MethodDeclarationSyntax methodDeclarationSyntax,
        SemanticModel semanticModel)
    {
        _projectModel.Logger.Information($"Looking for method invocations.");
        List<(string, string)> invocationDeclarations = new();
        
        // find the invocations
        var invocations = methodDeclarationSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>();

        // looking at each invocation
        foreach (var invocation in invocations)
        {
            // use the semantic model to do symbol resolving
            // to find the definition for the method being invoked
            var info = semanticModel.GetSymbolInfo(invocation);
            var methodSymbol = info.Symbol;
            
            // the symbol could be null
            // if the symbol is not defined in syntax trees that
            // are loaded in the csharp compilation
            if (methodSymbol != null)
            {
                // get the declaration for the invocation
                var declaration = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().ToString() ??
                                  string.Empty;
                invocationDeclarations.Add((invocation.ToString(), $"<<TUPLE>> {declaration}"));
            }
            else
            {
                invocationDeclarations.Add((invocation.ToString(), $"<<TUPLE>>"));
            }
        }

        _projectModel.Logger.Information($"Finished looking for method invocations.");

        return invocationDeclarations;
    }

    /// <summary>
    /// Creates a Test Class Record
    /// </summary>
    /// <param name="analysisProject">Current project we are working in</param>
    /// <param name="document">Current document being analyzed</param>
    /// <param name="namespaceDec">Namespace of the document</param>
    /// <param name="classDeclaration">Identifier of the class</param>
    /// <param name="fieldDeclarations">Fields declared in the class</param>
    /// <param name="usings">Using statements found in the document</param>
    /// <param name="testFramework">Test framework found in the document</param>
    /// <param name="sourceClass">Source class matched to the test class</param>
    /// <returns>TestClassRecord</returns>
    private TestClassRecord CreateTestClassRecord(AnalysisProject analysisProject, SyntaxTree document,
        string namespaceDec, ClassDeclarationSyntax classDeclaration, List<string> fieldDeclarations, List<string> usings,
        string testFramework, SyntaxTree sourceClass)
    {
        return new TestClassRecord
        (
            _projectModel.Owner,
            _projectModel.RepoName,
            analysisProject.SolutionFilePath,
            analysisProject.ProjectFilePath,
            document.FilePath,
            namespaceDec,
            classDeclaration.Identifier.ToString(),
            string.Join("", fieldDeclarations),
            string.Join("", usings),
            testFramework,
            analysisProject.LanguageFramework,
            classDeclaration.ToFullString().Trim(),
            classDeclaration.Span.Start.ToString(),
            classDeclaration.Span.End.ToString(),
            sourceClass.ToString()
        );
    }

    /// <summary>
    /// Creates a Test method record
    /// </summary>
    /// <param name="analysisProject">Current project we are working in</param>
    /// <param name="document">Current document being analyzed</param>
    /// <param name="namespaceDec">Namespace of the document</param>
    /// <param name="classDeclaration">Identifier of the class</param>
    /// <param name="fieldDeclarations">Fields declared in the class</param>
    /// <param name="usings">Using statements found in the document</param>
    /// <param name="method">Test method found</param>
    /// <param name="methodInvocations">Method called in the test method and their definitions</param>
    /// <returns></returns>
    private TestMethodRecord CreateTestMethodRecord(AnalysisProject analysisProject, SyntaxTree document,
        string namespaceDec, ClassDeclarationSyntax classDeclaration, List<string> fieldDeclarations, List<string> usings,
        (MethodDeclarationSyntax, string) method, List<(string, string)> methodInvocations)
    {
        return new TestMethodRecord
        (
            _projectModel.Owner,
            _projectModel.RepoName,
            analysisProject.SolutionFilePath,
            analysisProject.ProjectFilePath,
            document.FilePath,
            namespaceDec,
            classDeclaration.Identifier.ToString(),
            string.Join("", fieldDeclarations),
            string.Join("" ,usings),
            method.Item2,
            analysisProject.LanguageFramework,
            method.Item1.ToString(),
            method.Item1.Span.Start.ToString(),
            method.Item1.Span.End.ToString(),
            string.Join("",methodInvocations)
        );
    }

    /// <summary>
    /// Writes a test method record to the test method CSV
    /// </summary>
    /// <param name="testMethodRecord">Record containing the test method and source code (code being tested)</param>
    private void WriteResults(TestMethodRecord testMethodRecord)
    {
        List<TestMethodRecord> testMethodRecords = [testMethodRecord];
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = !File.Exists(_methodOutFile),
            Quote = '\'',
            // Write header only if file doesn't exist
        };
        using (var stream = File.Open(_methodOutFile, FileMode.Append))
        using (var writer = new StreamWriter(stream))
        using (var csv = new CsvWriter(writer, config))
        {
            try
            {
                csv.WriteRecords(testMethodRecords);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }

    /// <summary>
    /// Writes a test class record to the test class CSV
    /// </summary>
    /// <param name="testClassRecord">Record containing the test class and source code class (class being tested)</param>
    private void WriteResults(TestClassRecord testClassRecord)
    {
        List<TestClassRecord> testClassRecords = [testClassRecord];
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = !File.Exists(_methodOutFile),
            Quote = '\'',
            // Write header only if file doesn't exist
        };
        using (var stream = File.Open(_classOutFile, FileMode.Append))
        using (var writer = new StreamWriter(stream))
        using (var csv = new CsvWriter(writer, config))
        {
            try
            {
                csv.WriteRecords(testClassRecords);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }

    /// <summary>
    /// Formats the source code for the CSV
    /// </summary>
    /// <param name="str">String of source code to modify</param>
    /// <returns>Formatted string of source code</returns>
    private string ReplaceNewlines(string str)
    {
        return str.Replace("\r\n", "<<NEWLINE>>")
            .Replace("\r", "<<NEWLINE>>")
            .Replace("\n", "<<NEWLINE>>")
            .Replace("'", "<<SINGLE-QUOTE>>");
    }
}