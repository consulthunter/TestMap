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

        if (!File.Exists(path)) return;

        foreach (var line in File.ReadLines(path))
        {
            if (!TryParseEnvironmentVariable(line, out var key, out var value)) continue;

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static (string Owner, string Repo) ExtractOwnerAndRepo(string url)
    {
        var uri = new Uri(url);
        if (uri.Segments.Length < 3)
            throw new InvalidOperationException(
                $"Repository URL '{url}' does not include owner and repository segments.");

        var owner = uri.Segments[1].Trim('/');
        var repo = uri.Segments[2].Trim('/').Replace(".git", "", StringComparison.OrdinalIgnoreCase);
        return (owner, repo);
    }

    public static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeObjectIdentityHash(int fileId, string @namespace, string name, string kind)
    {
        return ComputeSha256(
            string.Join(
                "|",
                fileId,
                NormalizeIdentityPart(@namespace),
                NormalizeIdentityPart(name),
                NormalizeIdentityPart(kind)));
    }

    public static string ComputeMemberIdentityHash(int objectEntityId, string name, string kind, string fullString)
    {
        var signatureFingerprint = TryExtractMemberSignatureFingerprint(fullString);
        return ComputeSha256(
            string.Join(
                "|",
                objectEntityId,
                NormalizeIdentityPart(kind),
                NormalizeIdentityPart(name),
                signatureFingerprint));
    }

    public static string? ExtractTestMethodName(string testCode)
    {
        return TryExtractTestMethodName(testCode, out var methodName) ? methodName : null;
    }

    public static bool TryExtractTestMethodName(string testCode, out string? methodName)
    {
        methodName = null;

        try
        {
            var tree = CSharpSyntaxTree.ParseText(testCode);
            var root = tree.GetRoot();

            methodName = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Select(x => x.Identifier.Text)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(methodName)) return true;

            methodName = root.DescendantNodes()
                .OfType<LocalFunctionStatementSyntax>()
                .Select(x => x.Identifier.Text)
                .FirstOrDefault();

            return !string.IsNullOrWhiteSpace(methodName);
        }
        catch
        {
            methodName = null;
            return false;
        }
    }

    public static bool InsertTestIntoFile(string className, string filePath, string testMethodCode)
    {
        if (!File.Exists(filePath)) return false;

        var sourceText = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetCompilationUnitRoot();

        var classNode = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);

        if (classNode == null) return false;

        if (SyntaxFactory.ParseMemberDeclaration(testMethodCode) is not MethodDeclarationSyntax method) return false;

        var updatedClassNode = classNode.AddMembers(method);
        var updatedRoot = root.ReplaceNode(classNode, updatedClassNode);

        File.WriteAllText(filePath, updatedRoot.NormalizeWhitespace().ToFullString());
        return true;
    }

    private static bool TryParseEnvironmentVariable(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(line)) return false;

        var trimmed = line.Trim();
        if (trimmed.StartsWith("#", StringComparison.Ordinal)) return false;

        var parts = trimmed.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;

        key = parts[0].Trim();
        value = parts[1].Trim().Trim('"');
        return !string.IsNullOrWhiteSpace(key);
    }

    private static string NormalizeIdentityPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("\r", string.Empty).Replace("\n", " ");
    }

    private static string TryExtractMemberSignatureFingerprint(string fullString)
    {
        if (string.IsNullOrWhiteSpace(fullString)) return string.Empty;

        try
        {
            if (SyntaxFactory.ParseMemberDeclaration(fullString) is not MemberDeclarationSyntax declaration)
                return NormalizeIdentityPart(fullString);

            return declaration switch
            {
                MethodDeclarationSyntax method => BuildMethodFingerprint(
                    method.Identifier.Text,
                    method.TypeParameterList?.ToFullString(),
                    method.ParameterList.ToFullString()),
                ConstructorDeclarationSyntax constructor => BuildMethodFingerprint(
                    constructor.Identifier.Text,
                    string.Empty,
                    constructor.ParameterList.ToFullString()),
                DestructorDeclarationSyntax destructor => BuildMethodFingerprint(
                    destructor.Identifier.Text,
                    string.Empty,
                    destructor.ParameterList.ToFullString()),
                OperatorDeclarationSyntax op => BuildMethodFingerprint(
                    op.OperatorToken.Text,
                    string.Empty,
                    op.ParameterList.ToFullString()),
                ConversionOperatorDeclarationSyntax conversion => BuildMethodFingerprint(
                    conversion.Type.ToString(),
                    string.Empty,
                    conversion.ParameterList.ToFullString()),
                PropertyDeclarationSyntax property =>
                    $"{property.Identifier.Text}|{NormalizeIdentityPart(property.Type.ToString())}",
                IndexerDeclarationSyntax indexer =>
                    $"this|{NormalizeIdentityPart(indexer.Type.ToString())}|{NormalizeIdentityPart(indexer.ParameterList.ToFullString())}",
                FieldDeclarationSyntax field =>
                    $"{NormalizeIdentityPart(field.Declaration.Type.ToString())}|{NormalizeIdentityPart(string.Join(",", field.Declaration.Variables.Select(x => x.Identifier.Text)))}",
                EventFieldDeclarationSyntax eventField =>
                    $"{NormalizeIdentityPart(eventField.Declaration.Type.ToString())}|{NormalizeIdentityPart(string.Join(",", eventField.Declaration.Variables.Select(x => x.Identifier.Text)))}",
                EventDeclarationSyntax eventDeclaration =>
                    $"{NormalizeIdentityPart(eventDeclaration.Type.ToString())}|{NormalizeIdentityPart(eventDeclaration.Identifier.Text)}",
                EnumMemberDeclarationSyntax enumMember => NormalizeIdentityPart(enumMember.Identifier.Text),
                _ => NormalizeIdentityPart(declaration.ToString())
            };
        }
        catch
        {
            return NormalizeIdentityPart(fullString);
        }
    }

    private static string BuildMethodFingerprint(string name, string? typeParameters, string parameterList)
    {
        return string.Join(
            "|",
            NormalizeIdentityPart(name),
            NormalizeIdentityPart(typeParameters),
            NormalizeIdentityPart(parameterList));
    }
}