using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Editing;

public sealed class TestCodeEditService : ITestCodeEditService
{
    public bool EnsureTestClassExists(CandidateMethodContext context)
    {
        if (!File.Exists(context.TestFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(context.TestFilePath)!);
            File.WriteAllText(
                context.TestFilePath,
                BuildEmptyTestClass(context));
            return true;
        }

        var sourceText = File.ReadAllText(context.TestFilePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetCompilationUnitRoot();

        var classNode = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == context.TestClassName);

        if (classNode != null) return true;

        var newClass = CreateTestClassDeclaration(context);

        CompilationUnitSyntax updatedRoot;
        var namespaceNode = root.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceNode != null)
        {
            var updatedNamespace = namespaceNode.AddMembers(newClass);
            updatedRoot = root.ReplaceNode(namespaceNode, updatedNamespace);
        }
        else
        {
            updatedRoot = root.AddMembers(newClass);
        }

        File.WriteAllText(context.TestFilePath, updatedRoot.NormalizeWhitespace().ToFullString());
        return true;
    }

    public bool AppendTestMethod(CandidateMethodContext context, string testMethodCode)
    {
        if (!EnsureTestClassExists(context) || !TryParseMethod(testMethodCode, out var method)) return false;

        var root = ParseRoot(context.TestFilePath);
        var classNode = FindClass(root, context.TestClassName);
        if (classNode == null) return false;

        var updatedClassNode = classNode.AddMembers(method!);
        var updatedRoot = root.ReplaceNode(classNode, updatedClassNode);
        File.WriteAllText(context.TestFilePath, updatedRoot.NormalizeWhitespace().ToFullString());
        return true;
    }

    public bool ReplaceTestMethod(CandidateMethodContext context, string existingMethodName,
        string replacementTestMethodCode)
    {
        if (!EnsureTestClassExists(context) ||
            !TryParseMethod(replacementTestMethodCode, out var replacementMethod)) return false;

        var root = ParseRoot(context.TestFilePath);
        var classNode = FindClass(root, context.TestClassName);
        if (classNode == null) return false;

        var existingMethod = classNode.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(x => string.Equals(x.Identifier.Text, existingMethodName, StringComparison.Ordinal));

        if (existingMethod == null) return AppendTestMethod(context, replacementTestMethodCode);

        var updatedClassNode = classNode.ReplaceNode(existingMethod, replacementMethod!);
        var updatedRoot = root.ReplaceNode(classNode, updatedClassNode);
        File.WriteAllText(context.TestFilePath, updatedRoot.NormalizeWhitespace().ToFullString());
        return true;
    }

    private static CompilationUnitSyntax ParseRoot(string filePath)
    {
        var sourceText = File.ReadAllText(filePath);
        return CSharpSyntaxTree.ParseText(sourceText).GetCompilationUnitRoot();
    }

    private static ClassDeclarationSyntax? FindClass(CompilationUnitSyntax root, string className)
    {
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);
    }

    private static bool TryParseMethod(string testMethodCode, out MethodDeclarationSyntax? method)
    {
        method = SyntaxFactory.ParseMemberDeclaration(testMethodCode) as MethodDeclarationSyntax;
        return method != null;
    }

    private static string BuildEmptyTestClass(CandidateMethodContext context)
    {
        var namespaceDeclaration = string.IsNullOrWhiteSpace(context.TestNamespace)
            ? string.Empty
            : $"\nnamespace {context.TestNamespace};\n";

        return
            $@"{NormalizeDependencies(context.TestDependencies)}{namespaceDeclaration}

{BuildClassDeclarationText(context)}
{{
}}";
    }

    private static ClassDeclarationSyntax CreateTestClassDeclaration(CandidateMethodContext context)
    {
        var classDeclaration = SyntaxFactory.ClassDeclaration(context.TestClassName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

        var attributeList = BuildClassAttributeList(context.TestFramework);
        return attributeList == null
            ? classDeclaration
            : classDeclaration.AddAttributeLists(attributeList);
    }

    private static AttributeListSyntax? BuildClassAttributeList(string framework)
    {
        var attributeName = framework switch
        {
            "MSTest" => "TestClass",
            "NUnit" => "TestFixture",
            _ => null
        };

        return attributeName == null
            ? null
            : SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeName))));
    }

    private static string BuildClassDeclarationText(CandidateMethodContext context)
    {
        var classAttribute = context.TestFramework switch
        {
            "MSTest" => "[TestClass]\n",
            "NUnit" => "[TestFixture]\n",
            _ => string.Empty
        };

        return $"{classAttribute}public class {context.TestClassName}";
    }

    private static string NormalizeDependencies(string dependencies)
    {
        if (string.IsNullOrWhiteSpace(dependencies)) return "using System;";

        return dependencies.Trim();
    }
}