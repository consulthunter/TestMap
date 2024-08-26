using Microsoft.CodeAnalysis;

namespace TestMap.Models;

public class AnalysisProject
{
    public string ProjectFilePath;
    public Dictionary<string, SyntaxTree> SyntaxTrees;
    public List<string> ProjectReferences;
    public List<MetadataReference> Assemblies;
    public List<string> Documents;

    public AnalysisProject(Dictionary<string, SyntaxTree> syntaxTrees, List<string> projectReferences, List<MetadataReference> assemblies, 
        List<string> documents, string projectFilePath = "" )
    {
        SyntaxTrees = syntaxTrees;
        ProjectReferences = projectReferences;
        Assemblies = assemblies;
        Documents = documents;
        ProjectFilePath = projectFilePath;
    }
}