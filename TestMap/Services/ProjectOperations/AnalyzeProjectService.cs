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

using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Coverage;
using Location = TestMap.Models.Code.Location;

namespace TestMap.Services.ProjectOperations;

public class AnalyzeProjectService : IAnalyzeProjectService
{
    private readonly string _outFile;
    private readonly ProjectModel _projectModel;

    public AnalyzeProjectService(ProjectModel projectModel)
    {
        try
        {
            _projectModel = projectModel;
            _outFile = Path.Combine(_projectModel.OutputPath,
                $"data_{_projectModel.ProjectId}.jsonl");
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to load project model: {e.Message}");
        }
    }

    /// <summary>
    ///     Uses the compilation to create the semantic model
    ///     And gathers the necessary information to create
    ///     the test method and test class records
    /// </summary>
    /// <param name="analysisProject">Analysis project, project we are analyzing</param>
    /// <param name="cSharpCompilation">Csharp compilation for the project</param>
    public virtual async Task AnalyzeProjectAsync(AnalysisProject analysisProject, CSharpCompilation? cSharpCompilation)
    {
        _projectModel.Logger.Information($"Analyzing project {analysisProject.ProjectFilePath}");
        // for every .cs file in the current project
        foreach (var document in cSharpCompilation.SyntaxTrees)
        {
            if (document.FilePath.EndsWith(".g.cs") || document.FilePath.EndsWith("AssemblyAttributes.cs") ||
                document.FilePath.EndsWith("AssemblyInfo.cs"))
            {
                continue;
            }
            _projectModel.Logger.Information($"Analyzing {document.FilePath}");
            var compilation = cSharpCompilation;

            // Necessary to analyze types and retrieve declarations
            // for invocations
            var semanticModel = compilation.GetSemanticModel(document);

            var root = await document.GetRootAsync();

            var namespaceDec = FindNamespace(root);

            var usings = GetUsingStatements(root);

            var testingFramework = FindTestingFrameworkFromUsings(usings);
            
            CodeModel codeModel = new CodeModel(_projectModel.Owner, _projectModel.RepoName, 
                analysisProject.SolutionFilePath, analysisProject.ProjectFilePath, document.FilePath, 
                namespaceDec, usings, analysisProject.LanguageFramework);
            
            List<TestClass> testClasses = new List<TestClass>();

            var classDeclarationSyntaxes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            _projectModel.Logger.Information($"Number of class declarations: {classDeclarationSyntaxes.Count}");
            
            foreach (var classDeclaration in classDeclarationSyntaxes)
            {
                _projectModel.Logger.Information($"Class declaration: {classDeclaration.Identifier.ToString()}");
                TestClass testClass = new TestClass();
                var methodDeclarations = FindClassMethods(classDeclaration);
                ProcessClassMethods(testClass, methodDeclarations, semanticModel);

                // we only care about test classes, nonTestClasses will be found through symbolic resolution
                if (testClass.TestMethods.Any())
                {
                    _projectModel.Logger.Information($"Found {testClass.TestMethods.Count} methods");
                    var name =  classDeclaration.Identifier.ToString();
                    var body = classDeclaration.ToFullString().Trim();
                    var fieldDeclarations = FindClassFields(classDeclaration);
                    var attributes = FindClassAttributes(classDeclaration);
                    var modifiers = FindClassModifiers(classDeclaration);
                    Location location = new Location(classDeclaration.Span.Start, classDeclaration.Span.End);
                    
                    testClass.Name = name;
                    testClass.ClassBody = body;
                    testClass.ClassFields = fieldDeclarations;
                    testClass.Attributes = attributes;
                    testClass.Modifiers = modifiers;
                    testClass.Location = location;
                    
                    testClasses.Add(testClass);
                }
                
            }
            _projectModel.Logger.Information($"Finished analyzing {document.FilePath}");
            codeModel.TestClasses =  testClasses;
            WriteResults(codeModel);
        }
    }

    /// <summary>
    ///     Looks for the namespace defined in the document
    ///     using the root node
    /// </summary>
    /// <param name="rootNode">Root node of the document</param>
    /// <returns>String, namespace identifier if found</returns>
    private string FindNamespace(SyntaxNode rootNode)
    {
        _projectModel.Logger.Information("Looking for namespace.");
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
    ///     Looks for usings statements from the root node of the document
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
    ///     Searches the using statements for a testing framework
    ///     that is defined within the config
    /// </summary>
    /// <param name="usings">List of usings statements</param>
    /// <returns>Testing framework that matches from the config if present</returns>
    private string FindTestingFrameworkFromUsings(List<string> usings)
    {
        var testingFramework = "";
        _projectModel.Logger.Information("Looking for testing framework.");
        foreach (var usingStatement in usings)
        foreach (var framework in _projectModel.TestingFrameworks.Keys)
            if (usingStatement.ToLower().Contains(framework.ToLower()))
                testingFramework = framework;

        if (string.IsNullOrEmpty(testingFramework)) _projectModel.Logger.Warning("No testing framework found.");

        _projectModel.Logger.Information("Finished looking for testing framework.");
        return testingFramework;
    }

    private void ProcessSourceClass(TestClass testClass, SyntaxNode syntaxNode)
    {
        _projectModel.Logger.Information("Processing source class.");
        NonTestClass nonTestClass = new();
        var sourceSyntax = syntaxNode.SyntaxTree.GetRoot();
        var classDeclaration = sourceSyntax.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var ns = FindNamespace(sourceSyntax);
        var usingStatements = GetUsingStatements(sourceSyntax);
        if (classDeclaration != null)
        {
            var className = classDeclaration.Identifier.ToString();
            var classBody = classDeclaration.ToFullString();
            var attributes = FindClassAttributes(classDeclaration);
            var modifiers = FindClassModifiers(classDeclaration);
            Location location = new Location(classDeclaration.Span.Start, classDeclaration.Span.End);
            var filepath = classDeclaration.SyntaxTree.FilePath;
            ClassCoverage classCoverage = FindClassCoverageByName(className);
            nonTestClass.FilePath = filepath;
            nonTestClass.ClassBody = classBody;
            nonTestClass.Attributes = attributes;
            nonTestClass.Modifiers = modifiers;
            nonTestClass.Location = location;
            nonTestClass.Namespace = ns;
            nonTestClass.UsingStatements = usingStatements;
            nonTestClass.Coverage = classCoverage;
        
            testClass.AddIfNotPresent(nonTestClass);   
        }
    }

    private void ProcessSourceMethod(TestMethod testMethod, SyntaxNode syntaxNode)
    {
        _projectModel.Logger.Information("Processing source method.");
        try
        {
            var method = (MethodDeclarationSyntax)syntaxNode;
            string name = method.Identifier.ValueText;
            List<string> attributes = FindMethodAttributes(method);
            List<string> modifiers = FindMethodModifiers(method);
            string methodBody = "";
            Location location = new Location(0,0);
            if (method.Body != null)
            {
                methodBody = method.Body.ToString();
                location = new Location(method.Body.Span.Start, method.Body.Span.End);
            }
            List<string> invocations = FindInvocations(method);
            MethodCoverage methodCoverage = FindMethodCoverageByName(name);
            NonTestMethod nonTestMethod = new NonTestMethod(name, attributes, modifiers, invocations, methodBody, location, methodCoverage);
            
            testMethod.AddIfNotPresent(nonTestMethod);
        }
        catch (Exception ex)
        {
            _projectModel.Logger.Error($"Error processing syntax node: {ex.Message}");
        }
    }

    private List<string> FindClassAttributes(ClassDeclarationSyntax classDeclaration)
    {
        _projectModel.Logger.Information("Looking for class attributes.");
        List<string> attributes = new();
        
        foreach (var attribute in classDeclaration.AttributeLists)
        {
            attributes.Add(attribute.ToFullString());
        }
        return attributes;
    }
    private List<string> FindClassModifiers(ClassDeclarationSyntax classDeclaration)
    {
        _projectModel.Logger.Information("Looking for class modifiers.");
        List<string> modifiers = new();
        
        foreach (var modifier in classDeclaration.Modifiers)
        {
            modifiers.Add(modifier.ToFullString());
        }
        return modifiers;
    }
    
    /// <summary>
    ///     Looks for fields in the class
    /// </summary>
    /// <param name="classNode">Class node</param>
    /// <returns>List of strings, fields and properties defined in the class</returns>
    private List<string> FindClassFields(SyntaxNode classNode)
    {
        List<string> results = new();
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
    ///     Looks for method declaration syntax in the document
    /// </summary>
    /// <param name="classNode">Class node</param>
    /// <returns>List of tuples, (method, test framework)</returns>
    private List<MethodDeclarationSyntax> FindClassMethods(SyntaxNode classNode)
    {
        _projectModel.Logger.Information("Looking for method declarations.");
        var methods = classNode.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

        _projectModel.Logger.Information("Finished looking for test method declarations.");
        return methods;
    }

    private void ProcessClassMethods(TestClass testClass, List<MethodDeclarationSyntax> methods, SemanticModel semanticModel)
    {
        foreach (var method in methods)
        {
            _projectModel.Logger.Information("Processing method.");
            string name = method.Identifier.ValueText;
            List<string> attributes = FindMethodAttributes(method);
            List<string> modifiers = FindMethodModifiers(method);
            string methodBody = "";
            Location location = new Location(0,0);
            if (method.Body != null)
            {
                methodBody = method.Body.ToString();
                location = new Location(method.Body.Span.Start, method.Body.Span.End);
            }
            List<SyntaxNode> methodInvocationsWithSource = FindInvocationsWithSource(method, semanticModel);
            List<string> invocations = FindInvocations(method);
            (bool isTestMethod, string framework) = IsTestMethod(method);
            
            // if a test method
            // if the invocation is not in the same filepath as the method
            // this likely means that it is related to what's being tested
            // we could look through and directly map the source code information, we'd create a
            // new NonTestClass adding it to its methods, it would be good to track the source code filepath
            // 
            // or we could save it to another object for mapping later
            // we also need to find the assertions for that test if any
            // 
            // if the invocation is in the same path, then it should be picked up by another method
            // and we can skip it
            if (isTestMethod)
            {
                _projectModel.Logger.Information("Testing method found.");
                List<string> assertions = FindAssertions(method);
                
                TestMethod testMethod = new TestMethod(name, attributes, modifiers,  invocations,  methodBody, location, assertions);
                testClass.TestFramework =  framework;
                foreach (var methodInvocation in methodInvocationsWithSource)
                {
                    // They share the same file
                    // we can skip
                    if (methodInvocation.GetLocation().SourceTree?.FilePath == method.GetLocation().SourceTree?.FilePath)
                    {
                        continue;
                    }
                    // we need to add the test method
                    // otherwise we need to process the invocation as a NonTestClass and NonTestMethod
                    ProcessSourceClass(testClass, methodInvocation);
                    ProcessSourceMethod(testMethod, methodInvocation);
                }
                testClass.TestMethods.Add(testMethod);
                
            }
            // add to non-test-methods
            else
            {
                _projectModel.Logger.Information("Non-Testing method found.");
                MethodCoverage methodCoverage = FindMethodCoverageByName(name);
                NonTestMethod nonTestMethod = new NonTestMethod(name, attributes, modifiers, invocations, methodBody, location, methodCoverage);
                testClass.NonTestMethods.Add(nonTestMethod);
            }
        }
    }
    
    private ClassCoverage FindClassCoverageByName(string name)
    {
        _projectModel.Logger.Information("Looking for class coverage.");
        CoverageReport report = _projectModel.CoverageReport;

        foreach (var package in report.Packages)
        {
            foreach (var classCoverage in package.Classes)
            {
                if (classCoverage.Name == name)
                {
                    return classCoverage;
                }

            }
        }
        return new ClassCoverage();
    }

    private MethodCoverage FindMethodCoverageByName(string name)
    {
        _projectModel.Logger.Information("Looking for method coverage.");
        CoverageReport report = _projectModel.CoverageReport;

        foreach (var package in report.Packages)
        {
            foreach (var classCoverage in package.Classes)
            {
                foreach (var method in classCoverage.Methods)
                {
                    if (method.Name == name)
                    {
                        return method;
                    }
                    
                }
                
            }
        }
        return new MethodCoverage();
    }

    private List<string> FindMethodAttributes(MethodDeclarationSyntax method)
    {
        List<string> attributes = new();
        _projectModel.Logger.Information("Looking for method attributes.");

        foreach (var attribute in method.AttributeLists)
        {
            attributes.Add(attribute.ToFullString());
        }
        return attributes;
    }

    private List<string> FindMethodModifiers(MethodDeclarationSyntax method)
    {
        List<string> modifiers = new();
        _projectModel.Logger.Information("Looking for method modifiers.");

        foreach (var variableModifier in method.Modifiers)
        {
            modifiers.Add(variableModifier.ToFullString());
        }
        return modifiers;
    }

    private List<string> FindAssertions(MethodDeclarationSyntax method)
    {
        List<string> assertions = new();
        _projectModel.Logger.Information("Looking for assertions.");
        // find the invocations
        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.ToFullString().Contains("Assert"))
            {
                assertions.Add(invocation.ToFullString());
            }
        }

        return assertions;
    }
    /// <summary>
    /// Determines if the method is a test method using the attributes defined in the config file.
    /// </summary>
    /// <param name="methodDeclarationSyntax">Method defined in the document.</param>
    /// <returns>Tuple: (boolean if the attribute is in the defined list, testingFramework or empty string).</returns>
    private (bool, string) IsTestMethod(MethodDeclarationSyntax methodDeclarationSyntax)
    {
        // Iterate through all testing frameworks in the project model
        foreach (var framework in _projectModel.TestingFrameworks)
        {
            var frameworkName = framework.Key; // Framework name (e.g., "NUnit", "xUnit")
            var frameworkAttributes = framework.Value; // List of attributes for the framework

            // Check if the method has any attributes from the current framework
            var hasAttribute = methodDeclarationSyntax.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => frameworkAttributes.Contains(attr.Name.ToString()));

            if (hasAttribute)
            {
                return (true, frameworkName); // Return true and the framework name
            }
        }

        return (false, string.Empty); // Return false if no matching attribute is found
    }
    /// <summary>
    ///     Looks for method invocations within the test method
    /// </summary>
    /// <param name="methodDeclarationSyntax">Test method</param>
    /// <param name="semanticModel">Semantic model</param>
    /// <returns>List of tuples, (method invocation, method definition)</returns>
    private List<string> FindInvocations(MethodDeclarationSyntax methodDeclarationSyntax)
    {
        _projectModel.Logger.Information("Looking for method invocations.");
        List<string> invocationDeclarations = new();

        // find the invocations
        var invocations = methodDeclarationSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>();

        // looking at each invocation
        foreach (var invocation in invocations)
        {
            invocationDeclarations.Add(invocation.Expression.ToString());
        }

        _projectModel.Logger.Information("Finished looking for method invocations.");

        return invocationDeclarations;
    }
    /// <summary>
    ///     Looks for method invocations within the test method
    /// </summary>
    /// <param name="methodDeclarationSyntax">Test method</param>
    /// <param name="semanticModel">Semantic model</param>
    /// <returns>List of tuples, (method invocation, method definition)</returns>
    private List<SyntaxNode> FindInvocationsWithSource(MethodDeclarationSyntax methodDeclarationSyntax,
        SemanticModel semanticModel)
    {
        _projectModel.Logger.Information("Looking for method invocations.");
        List<SyntaxNode> invocationDeclarations = new();

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
                // what needs to happen here,
                // if not null we need to keep a list of these items and return
                var declaration = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

                if (declaration != null)
                {
                    invocationDeclarations.Add(declaration);
                }
            }
        }

        _projectModel.Logger.Information("Finished looking for method invocations.");

        return invocationDeclarations;
    }
    

    /// <summary>
    ///     Writes a test method record to the test method CSV
    /// </summary>
    /// <param name="testMethod">Record containing the test method and source code (code being tested)</param>
    private void WriteResults(CodeModel model)
    {
        using StreamWriter writer = new(_outFile, append: true);


        if (model.TestClasses.Count > 0)
        {
            string json = JsonSerializer.Serialize(model);
            writer.WriteLine(json);
        }
    }
}