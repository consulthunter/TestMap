using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models.Code;

namespace TestMap.Services.TestGeneration.TargetSelection;

internal static class ContainingTypeIdentity
{
    public static string Resolve(ObjectModel sourceObject, string sourceFilePath)
    {
        var fallback = Qualify(sourceObject.Namespace, sourceObject.Name);
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            return fallback;

        try
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFilePath)).GetCompilationUnitRoot();
            var declaration = root.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Where(x => x.Identifier.Text.Equals(sourceObject.Name, StringComparison.Ordinal))
                .FirstOrDefault(x =>
                    x.GetLocation().GetLineSpan().StartLinePosition.Line == sourceObject.Location.StartLineNumber);
            if (declaration == null) return fallback;

            var typePath = declaration.AncestorsAndSelf()
                .OfType<BaseTypeDeclarationSyntax>()
                .Reverse()
                .Select(x => x.Identifier.Text);
            return Qualify(sourceObject.Namespace, string.Join('.', typePath));
        }
        catch
        {
            return fallback;
        }
    }

    private static string Qualify(string sourceNamespace, string typeName)
    {
        return string.IsNullOrWhiteSpace(sourceNamespace)
            ? typeName
            : $"{sourceNamespace}.{typeName}";
    }
}
