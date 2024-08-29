using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class BuildProjectService
{
    private ProjectModel _projectModel;

    public BuildProjectService(ProjectModel projectModel)
    {
        _projectModel = projectModel;
    }
    
    public virtual CSharpCompilation BuildProjectCompilation(AnalysisProject project)
    {
        return CreateProjectCompilation(project);
    }
    
    private CSharpCompilation CreateProjectCompilation(AnalysisProject project)
    {
        _projectModel.Logger.Information($"Creating {project.ProjectFilePath} compilation.");
        // creates a random filename for the temporary assembly
        string assemblyName = Path.GetRandomFileName();

        List<SyntaxTree> trees = project.SyntaxTrees.Values.ToList();
        List<MetadataReference> assemblies = project.Assemblies;

        foreach (var reference in project.ProjectReferences)
        {
            AnalysisProject temp = _projectModel.Projects
                .First(proj => proj.ProjectFilePath == reference);

            // for every document look at the syntax tree
            // add if not present
            foreach (var doc in temp.Documents)
            {
                if (!trees.Contains(temp.SyntaxTrees[doc]) && !(doc.Contains(".AssemblyInfo.cs") || doc.Contains(".AssemblyAttributes.cs")))
                {
                    trees.Add(temp.SyntaxTrees[doc]);
                }
            }
        }

        // Create a compilation of the current project
        var compilation = CSharpCompilation.Create(assemblyName,
            syntaxTrees: trees,
            references: assemblies,
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        var diagnostics = compilation.GetDiagnostics();
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            foreach (var diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                _projectModel.Logger.Error($"{diagnostic}");
            }
        }

        _projectModel.Logger.Information($"Finished {project.ProjectFilePath} compilation.");
        return compilation;
    }
}