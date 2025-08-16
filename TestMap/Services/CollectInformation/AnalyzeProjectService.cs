/*
 * consulthunter
 * 2024-11-07
 * Looks at the syntaxTrees
 * for test methods and test classes
 *
 * Data is written from the code model
 * using JSONL format
 * 
 * AnalyzeProjectService.cs
 */

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Services.Database;
using TestMap.Services.ProjectOperations;
using Location = TestMap.Models.Code.Location;

namespace TestMap.Services.CollectInformation;

public class AnalyzeProjectService : IAnalyzeProjectService
{

    private readonly ProjectModel _projectModel;
    private Dictionary<string, List<InvocationModel>> _invocations = new();
    private Dictionary<string, MethodModel> _methods = new();
    private SqliteDatabaseService _databaseService;


    public AnalyzeProjectService(ProjectModel projectModel, SqliteDatabaseService databaseService)
    {
        _projectModel = projectModel;
        _databaseService = databaseService;
    }

    /// <summary>
    ///     Uses the compilation to create the semantic model
    ///     And gathers the necessary information for the code model
    /// </summary>
    /// <param name="analysisProject">Analysis project, project we are analyzing</param>
    /// <param name="cSharpCompilation">Csharp compilation for the project</param>
    public virtual async Task AnalyzeProjectAsync(AnalysisProject analysisProject, CSharpCompilation? cSharpCompilation)
    {
        
        _projectModel.Logger?.Information($"Analyzing project {analysisProject.ProjectFilePath}");
        // for every .cs file in the current project
        if (cSharpCompilation != null)
            foreach (var document in cSharpCompilation.SyntaxTrees)
            {
                if (document.FilePath.EndsWith(".g.cs") || document.FilePath.EndsWith("AssemblyAttributes.cs") ||
                    document.FilePath.EndsWith("AssemblyInfo.cs"))
                {
                    continue;
                }

                _projectModel.Logger?.Information($"Analyzing {document.FilePath}");

                // Necessary to analyze types and retrieve declarations
                // for invocations
                // var semanticModel = compilation.GetSemanticModel(document);

                var root = await document.GetRootAsync();

                var namespaceDec = FindNamespace(root);

                var usings = GetUsingStatements(root);
                
                var stringUsings = usings.Select(u => u.FullString).ToList();

                var testFramework = FindTestingFrameworkFromUsings(usings);
                
                
                // check to insert package, get id
                PackageModel package = new PackageModel(new List<FileModel>(), analysisProject.Id, Guid.NewGuid().ToString(), namespaceDec, Path.GetDirectoryName(document.FilePath) ?? "");
                await _databaseService.InsertPackageGetId(package);
                
                // check to insert file, get id
                FileModel file = new FileModel(stringUsings, package.Id, Guid.NewGuid().ToString(), namespaceDec, document.FilePath, cSharpCompilation.Language,
                    analysisProject.SolutionFilePath, analysisProject.ProjectFilePath, document.FilePath);
                await _databaseService.InsertFileGetId(file);
                
                // update usings
                usings.ForEach(u => u.FileId = file.Id);
                
                
                var classDeclarationSyntaxes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
                _projectModel.Logger?.Information($"Number of class declarations: {classDeclarationSyntaxes.Count}");

                foreach (var classDeclaration in classDeclarationSyntaxes)
                {
                    _projectModel.Logger?.Information($"Class declaration: {classDeclaration.Identifier.ToString()}");
                    var name = classDeclaration.Identifier.ToString();
                    var modifiers = FindClassModifiers(classDeclaration);
                    var attr = FindClassAttributes(classDeclaration);
                    var methods = FindClassMethods(classDeclaration);
                    var properties = FindClassFields(classDeclaration);
                    var visibility = GetVisibility(modifiers);
                    var fullString = classDeclaration.ToFullString().Trim();
                    var docString = GetDocComment(classDeclaration);
                    bool isTestClass = methods.Any(m => m.IsTestMethod);
                    
                    // insert class get id
                    ClassModel classModel = new ClassModel(file.Id, Guid.NewGuid().ToString(), name, visibility, attr, modifiers, fullString, docString, isTestClass, testFramework);
                    await _databaseService.InsertClassesGetId(classModel);
                    
                    methods.ForEach(method => method.ClassId = classModel.Id);
                    foreach (var method in methods)
                    {
                        await _databaseService.InsertMethodsGetId(method);
                    }
                    
                    // Iterate over the _methods dictionary
                    foreach (var methodKey in _methods.Keys)
                    {
                        // Access the MethodModel for the current key
                        MethodModel methodModel = _methods[methodKey];

                        // Check if there are invocations associated with the current methodKey
                        if (_invocations.ContainsKey(methodKey))
                        {
                            var invocationList = _invocations[methodKey];

                            // Modify each invocation model in the list
                            foreach (var invocationModel in invocationList)
                            {
                                // You now have access to both the MethodModel and InvocationModel
                                // Modify the invocationModel based on some condition involving methodModel
                                invocationModel.TargetMethodId = methodModel.Id;
                            }
                        }
                    }

                    foreach (var guid in _invocations.Keys)
                    {
                        var invocationList = _invocations[guid];

                        foreach (var invocationModel in invocationList)
                        {
                            await _databaseService.InsertInvocationsGetId(invocationModel);
                        }
                    }
                    
                    properties.ForEach(property => property.ClassId = classModel.Id);

                    foreach (var property in properties)
                    {
                        await _databaseService.InsertPropertyGetId(property);
                    }
                    
                }
                foreach (var import in usings)
                {
                    await _databaseService.InsertImports(import);
                }
            }
        _methods.Clear();
        _invocations.Clear();
    }
    
    /// <summary>
    ///     Looks for the namespace defined in the document
    ///     using the root node
    /// </summary>
    /// <param name="rootNode">Root node of the document</param>
    /// <returns>String, namespace identifier if found</returns>
    private string FindNamespace(SyntaxNode rootNode)
    {
        _projectModel.Logger?.Information("Looking for namespace.");
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
                _projectModel.Logger?.Warning("No namespace found.");
        }

        _projectModel.Logger?.Information("Finished looking for namespace.");
        return namespaceDec;
    }
    
    /// <summary>
    ///     Looks for usings statements from the root node of the document
    /// </summary>
    /// <param name="rootNode">Root node of the document</param>
    /// <returns>List of strings, using statements</returns>
    private List<ImportModel> GetUsingStatements(SyntaxNode rootNode)
    {
        _projectModel.Logger?.Information("Looking for using statements.");
        List<ImportModel> usingStatements = new();

        // Get all using directives
        var usingDirectives = rootNode.DescendantNodes().OfType<UsingDirectiveSyntax>();
        foreach (var usingDirective in usingDirectives)
            if (usingDirective.Name != null)
            {
                ImportModel import = new ImportModel(0, Guid.NewGuid().ToString(), usingDirective.Name.ToString(), 
                    usingDirective.Name.ToString(), usingDirective.ToString());
                usingStatements.Add(import);
            }

        _projectModel.Logger?.Information($"Number of using statements found. {usingStatements.Count}");
        _projectModel.Logger?.Information("Finished looking for using statements.");
        return usingStatements;
    }
    
    /// <summary>
    ///     Searches the using statements for a testing framework
    ///     that is defined within the config
    /// </summary>
    /// <param name="usings">List of usings statements</param>
    /// <returns>Testing framework that matches from the config if present</returns>
    private string FindTestingFrameworkFromUsings(List<ImportModel> usings)
    {
        var testingFramework = "";
        _projectModel.Logger?.Information("Looking for testing framework.");
        foreach (var usingStatement in usings)
            if (_projectModel.TestingFrameworks != null)
                foreach (var framework in _projectModel.TestingFrameworks.Keys)
                    if (usingStatement.FullString.ToLower().Contains(framework.ToLower()))
                        testingFramework = framework;

        if (string.IsNullOrEmpty(testingFramework)) _projectModel.Logger?.Warning("No testing framework found.");

        _projectModel.Logger?.Information("Finished looking for testing framework.");
        return testingFramework;
    }
    public string GetVisibility(List<string> modifiers)
    {
        // If the list of modifiers is empty, assume it's internal (default visibility)
        if (modifiers.Count == 0)
        {
            return "internal";
        }

        // Iterate through the modifiers and check for known access modifiers
        foreach (var modifier in modifiers)
        {
            var modifierText = modifier.ToLower().Trim();

            if (modifierText == "public")
            {
                return "public";
            }
            else if (modifierText == "private")
            {
                return "private";
            }
            else if (modifierText == "protected")
            {
                return "protected";
            }
            else if (modifierText == "internal")
            {
                return "internal";
            }
            else if (modifierText == "protectedinternal")
            {
                return "protected internal";
            }
            else if (modifierText == "privateprotected")
            {
                return "private protected";
            }
        }

        // If no access modifier found, return "internal" as a default
        return "internal";
    }
    public string GetDocComment(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia();
        var docComment = trivia.FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        if (docComment != default)
        {
            // Extract the XML documentation comment
            var xmlDocComment = docComment.GetStructure()?.ToString();
            return xmlDocComment ?? String.Empty;
        }
        return String.Empty;
    }
    
    /// <summary>
    ///     Collects attributes for a class declaration
    ///     i.e the [] before the declaration
    /// </summary>
    /// <param name="classDeclaration"></param>
    /// <returns>List of attributes</returns>
    private List<string> FindClassAttributes(ClassDeclarationSyntax classDeclaration)
    {
        _projectModel.Logger?.Information("Looking for class attributes.");
        List<string> attributes = new();
        
        foreach (var attribute in classDeclaration.AttributeLists)
        {
            attributes.Add(attribute.ToFullString());
        }
        return attributes;
    }
    /// <summary>
    ///     Collects modifiers for a class declaration
    ///     such as public, private, static, partial, etc.
    /// </summary>
    /// <param name="classDeclaration"></param>
    /// <returns>List of modifiers</returns>
    private List<string> FindClassModifiers(ClassDeclarationSyntax classDeclaration)
    {
        _projectModel.Logger?.Information("Looking for class modifiers.");
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
    private List<PropertyModel> FindClassFields(SyntaxNode classNode)
    {
        List<PropertyModel> results = new();
        _projectModel.Logger?.Information("Looking for fields.");
        var properties = classNode.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
        var fields = classNode.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
        foreach (var property in properties)
        {
            var name = property.Identifier.ToFullString().Trim();
            var modifiers = FindPropertyModifiers(property);
            var attr = FindPropertyAttributes(property);
            var visibility = GetVisibility(modifiers);
            var fullString = property.ToFullString().Trim();
            var spec = property.GetLocation().GetLineSpan();
            Location location = new Location(spec.StartLinePosition.Line, spec.StartLinePosition.Character, spec.EndLinePosition.Line, spec.EndLinePosition.Character);
            PropertyModel propertModel = new PropertyModel(0, Guid.NewGuid().ToString(), name, visibility, attr, modifiers, fullString, location);
            results.Add(propertModel);
        }
        
        foreach (var field in fields)
        {
            var name = field.Declaration.Variables[0].Identifier.ToFullString().Trim();
            var modifiers = FindFieldModifiers(field);
            var attr = FindFieldAttributes(field);
            var visibility = GetVisibility(modifiers);
            var fullString = field.ToFullString().Trim();
            var spec = field.GetLocation().GetLineSpan();
            Location location = new Location(spec.StartLinePosition.Line, spec.StartLinePosition.Character, spec.EndLinePosition.Line, spec.EndLinePosition.Character);
            PropertyModel propertModel = new PropertyModel(0, Guid.NewGuid().ToString(), name, visibility, attr, modifiers, fullString, location);
            results.Add(propertModel);
        }
        _projectModel.Logger?.Information($"Number of field declarations: {results.Count}.");
        _projectModel.Logger?.Information("Finished looking for fields.");
        return results;
    }
    
    /// <summary>
    ///     Looks for method declaration syntax in the document
    /// </summary>
    /// <param name="classNode">Class node</param>
    /// <returns>List of Methods)</returns>
    private List<MethodModel> FindClassMethods(SyntaxNode classNode)
    {
        List<MethodModel> methods = new();
        _projectModel.Logger?.Information("Looking for method declarations.");
        var methodDeclarationSyntaxes = classNode.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

        foreach (var methodDeclarationSyntax in methodDeclarationSyntaxes)
        {
            var name = methodDeclarationSyntax.Identifier.ToFullString().Trim();
            var modifiers = FindMethodModifiers(methodDeclarationSyntax);
            var attr = FindMethodAttributes(methodDeclarationSyntax);
            var visibility = GetVisibility(modifiers);
            var fullString = methodDeclarationSyntax.ToFullString().Trim();
            var docstring = GetDocComment(methodDeclarationSyntax);
            (bool isTest, string framework) = IsTestMethod(methodDeclarationSyntax);
            var spec = methodDeclarationSyntax.GetLocation().GetLineSpan();
            Location location = new Location(spec.StartLinePosition.Line, spec.StartLinePosition.Character,
                spec.EndLinePosition.Line, spec.EndLinePosition.Character);
            MethodModel method = new MethodModel(0, Guid.NewGuid().ToString(), name, visibility, attr, modifiers, fullString, docstring, isTest, framework, location);
            FindInvocations(methodDeclarationSyntax, method.Guid);
            methods.Add(method);
            
            // Use the methodGuid or another key to store the method in the dictionary
            string key = method.Guid;

            _methods.TryAdd(key, method);
        }

        _projectModel.Logger?.Information("Finished looking for test method declarations.");
        return methods;
    }
    /// <summary>
    ///     Finds a list of method attributes such as [Test]
    /// </summary>
    /// <param name="method">MethodDeclarationSyntax, representation of the method</param>
    /// <returns>List of string attributes</returns>
    private List<string> FindMethodAttributes(MethodDeclarationSyntax method)
    {
        List<string> attributes = new();
        _projectModel.Logger?.Information("Looking for method attributes.");

        foreach (var attribute in method.AttributeLists)
        {
            attributes.Add(attribute.ToFullString());
        }
        return attributes;
    }
    
    /// <summary>
    ///     Determines if the method is a test method using the attributes defined in the config file.
    /// </summary>
    /// <param name="methodDeclarationSyntax">Method defined in the document.</param>
    /// <returns>Tuple: (boolean if the attribute is in the defined list, testingFramework or empty string).</returns>
    private (bool, string) IsTestMethod(MethodDeclarationSyntax methodDeclarationSyntax)
    {
        // Iterate through all testing frameworks in the project model
        if (_projectModel.TestingFrameworks != null)
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
    ///     Finds a list of method modifiers such as public, void, static, etc.
    /// </summary>
    /// <param name="method">MethodDeclarationSyntax, representation of the method</param>
    /// <returns>List of string modifiers</returns>
    private List<string> FindMethodModifiers(MethodDeclarationSyntax method)
    {
        List<string> modifiers = new();
        _projectModel.Logger?.Information("Looking for method modifiers.");

        foreach (var variableModifier in method.Modifiers)
        {
            modifiers.Add(variableModifier.ToFullString());
        }
        return modifiers;
    }
    /// <summary>
    ///     Finds a list of method modifiers such as public, void, static, etc.
    /// </summary>
    /// <param name="method">MethodDeclarationSyntax, representation of the method</param>
    /// <returns>List of string modifiers</returns>
    private List<string> FindPropertyModifiers(PropertyDeclarationSyntax property)
    {
        List<string> modifiers = new();
        _projectModel.Logger?.Information("Looking for property modifiers.");

        foreach (var variableModifier in property.Modifiers)
        {
            modifiers.Add(variableModifier.ToFullString());
        }
        return modifiers;
    }
    /// <summary>
    ///     Finds a list of method modifiers such as public, void, static, etc.
    /// </summary>
    /// <param name="method">MethodDeclarationSyntax, representation of the method</param>
    /// <returns>List of string modifiers</returns>
    private List<string> FindPropertyAttributes(PropertyDeclarationSyntax property)
    {
        List<string> attributes = new();
        _projectModel.Logger?.Information("Looking for property attributes.");

        foreach (var attribute in property.AttributeLists)
        {
            attributes.Add(attribute.ToFullString());
        }
        return attributes;
    }
    
    /// <summary>
    ///     Finds a list of method modifiers such as public, void, static, etc.
    /// </summary>
    /// <param name="method">MethodDeclarationSyntax, representation of the method</param>
    /// <returns>List of string modifiers</returns>
    private List<string> FindFieldModifiers(FieldDeclarationSyntax field)
    {
        List<string> modifiers = new();
        _projectModel.Logger?.Information("Looking for field modifiers.");

        foreach (var variableModifier in field.Modifiers)
        {
            modifiers.Add(variableModifier.ToFullString());
        }
        return modifiers;
    }
    /// <summary>
    ///     Finds a list of method modifiers such as public, void, static, etc.
    /// </summary>
    /// <param name="method">MethodDeclarationSyntax, representation of the method</param>
    /// <returns>List of string modifiers</returns>
    private List<string> FindFieldAttributes(FieldDeclarationSyntax field)
    {
        List<string> attributes = new();
        _projectModel.Logger?.Information("Looking for field attributes.");

        foreach (var attribute in field.AttributeLists)
        {
            attributes.Add(attribute.ToFullString());
        }
        return attributes;
    }
    
    /// <summary>
    ///     Looks for method invocations within the test method
    /// </summary>
    /// <param name="methodDeclarationSyntax">Test method</param>
    /// <returns>List of tuples, (method invocation, method definition)</returns>
    private void FindInvocations(MethodDeclarationSyntax methodDeclarationSyntax, string methodGuid = "")
    {
        _projectModel.Logger?.Information("Looking for method invocations.");

        // Find the invocations
        var invocations = methodDeclarationSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var spec = invocation.GetLocation().GetLineSpan();
            Location location = new Location(spec.StartLinePosition.Line, spec.StartLinePosition.Character, spec.EndLinePosition.Line, spec.EndLinePosition.Character);

            // Create the invocation model
            InvocationModel invocationModel = new InvocationModel(0, 0, Guid.NewGuid().ToString(), invocation.ToFullString().Contains("Assert"), invocation.ToFullString().Trim(), location);

            // Use the methodGuid or another key to store the invocation in the dictionary
            string key = methodGuid;

            // Check if the key already exists in the dictionary
            if (!_invocations.ContainsKey(key))
            {
                _invocations[key] = new List<InvocationModel>();  // Create a new list if the key doesn't exist
            }

            // Add the invocation model to the list under the given key
            _invocations[key].Add(invocationModel);
        }
    }
    
}