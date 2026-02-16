using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestMap.Utilities;

public static class Utilities
{
    public static void Load()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), ".env");

        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static (string, string) ExtractOwnerAndRepo(string url)
    {
        var uri = new Uri(url);
        return (uri.Segments[1].Trim('/'), uri.Segments[2].Trim('/'));
    }
    
    public static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    
    public static string? ExtractTestMethodName(string testCode)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(testCode);
            var root = tree.GetRoot();

            // Try to find method declaration (handles both standard and local methods)
            var method = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (method != null)
                return method.Identifier.Text;

            // Fallback to local function
            var localMethod = root.DescendantNodes()
                .OfType<LocalFunctionStatementSyntax>()
                .FirstOrDefault();

            return localMethod?.Identifier.Text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to extract test method name: {ex.Message}");
            return null;
        }
    }

    public static void InsertTestIntoFile(string className, string filePath, string testMethodCode)
    {
        var sourceText = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetCompilationUnitRoot();

        var classNode = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);

        if (classNode == null)
            throw new InvalidOperationException($"Class '{className}' not found in {filePath}");

        // Parse the method text into syntax
        var method = SyntaxFactory.ParseMemberDeclaration(testMethodCode)
                     ?? throw new InvalidOperationException("Invalid test method syntax.");

        var newClassNode = classNode.AddMembers(method);

        var newRoot = root.ReplaceNode(classNode, newClassNode);

        File.WriteAllText(filePath, newRoot.NormalizeWhitespace().ToFullString());
    }
}