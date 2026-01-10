using System.Security.Cryptography;
using System.Text;
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

    public static void InsertTestIntoFile(string filePath, string test)
    {
        var file = File.ReadAllLines(filePath).ToList();
        var lastBraceIndex = file.FindLastIndex(line => line.Trim() == "}");
        if (lastBraceIndex == -1)
            throw new InvalidOperationException($"No closing brace found in {filePath}");

        file.Insert(lastBraceIndex, test);
        File.WriteAllLines(filePath, file);
    }
}