/*
 * consulthunter
 * 2024-11-07
 * Finds and loads solutions
 * and their projects
 * BuildSolutionService.cs
 */

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class BuildSolutionService(ProjectModel projectModel) : IBuildSolutionService
{
    /// <summary>
    /// Entry point for service
    /// </summary>
    public async Task BuildSolutionsAsync()
    {
        await FindAllSolutionFilesAsync();
    }
    
    /// <summary>
    /// Does a recursive search looking for
    /// (.sln) files in the repo
    /// </summary>
    private async Task FindAllSolutionFilesAsync()
    {
        if (Path.Exists(projectModel.DirectoryPath))
        {
            var solutions = Directory.GetFiles(projectModel.DirectoryPath, "*.sln", SearchOption.AllDirectories);

            // we work through all of the solutions
            // then do the projects outside the loop
            foreach (var solution in solutions)
            {
                projectModel.Logger.Information($"Solution file found: {solution}");
                // await BuildSolutionAsync(solution);
                await LoadSolutionAsync(solution);
            }
        }
    }
    
    /// <summary>
    /// DEPRECATED
    ///
    /// Used to clean, build, and restore the project
    /// Ran into issues with different SDKs
    /// Also, not entirely necessary
    /// </summary>
    /// <param name="solutionPath"></param>
    private async Task BuildSolutionAsync(string solutionPath)
    {
        var runner = new ScriptRunner();
        
        // clean project
        projectModel.Logger.Information($"Cleaning solution {solutionPath}");
        await runner.RunScriptAsync([solutionPath], projectModel.Scripts["Clean"]);
        projectModel.Logger.Information($"Cleaning {solutionPath} finished.");
        
        // restore
        projectModel.Logger.Information($"Restoring solution {solutionPath}");
        await runner.RunScriptAsync([solutionPath], projectModel.Scripts["Restore"]);
        projectModel.Logger.Information($"Restoring {solutionPath} finished.");
        
        // build
        projectModel.Logger.Information($"Building solution {solutionPath}");
        await runner.RunScriptAsync([solutionPath], projectModel.Scripts["Build"]);
        projectModel.Logger.Information($"Building {solutionPath} finished.");

        if (runner.HasError)
            foreach (var e in runner.Errors)
                projectModel.Logger.Error(e);
    }

    /// <summary>
    /// Opens the solution and loads the projects
    /// associated with the solution
    /// </summary>
    /// <param name="solutionPath">Absolute path to the solution (.sln) file</param>
    private async Task LoadSolutionAsync(string solutionPath)
    {
        try
        {
            projectModel.Logger.Information($"Loading solution {solutionPath}");
            using (var workspace = MSBuildWorkspace.Create())
            {
                var solution = await workspace.OpenSolutionAsync(solutionPath);
                
                var projects = solution.Projects.ToList();
                
                
                List<string> solutionProjects = new();

                foreach (var project in projects)
                {
                    // the compilation gives us access to SemanticModeling 
                    // and symbol resolving
                    var compilation = (CSharpCompilation) await project.GetCompilationAsync();
                    
                    // target framework is typically defined within the project (.csproj) file
                    var doc = XDocument.Load(project.FilePath);
                    var targetFramework = doc.Descendants("TargetFramework").FirstOrDefault()?.Value;
                    if (targetFramework == null)
                    {
                        targetFramework = doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
                    }

                    List<string> documents = GetDocuments(project);
                    Dictionary<string, SyntaxTree>? syntaxTrees = await GetSyntaxTrees(project, documents);
                    
                    AnalysisProject analysisProject = new AnalysisProject(solutionFilePath: solution.FilePath, projectReferences: GetProjectReferences(solution, project),
                        syntaxTrees: syntaxTrees, projectFilePath: project.FilePath, compilation: compilation, languageFramework: targetFramework);

                    if (project.FilePath != null)
                    {
                        var filepath = project.FilePath;

                        if (!projectModel.Projects.Exists(proj => proj.ProjectFilePath == filepath) &&
                            !solutionProjects.Exists(path => path == filepath))
                        {
                            projectModel.Projects.Add(analysisProject);
                            solutionProjects.Add(filepath);
                        }
                        else
                        {
                            projectModel.Logger.Warning($"Project {project.Id} filepath already exists in the list.");
                        }
                    }
                    else
                    {
                        // If filepath already exists in addedProjects, skip adding
                        projectModel.Logger.Warning($"Project {project.Id} filepath is null.");
                    }
                }

                if (!projectModel.Solutions.Exists(sol => sol.Solution.FilePath == solution.FilePath))
                {
                    var analysisSolution = new AnalysisSolution(solution, solutionProjects);
                    projectModel.Solutions.Add(analysisSolution);
                }
                else
                {
                    projectModel.Logger.Warning($"Solution {solution.FilePath} already exists in the list.");
                }
            }
        }
        catch (Exception ex)
        {
            projectModel.Logger.Error(ex.Message);
        }

        projectModel.Logger.Information($"Loading {solutionPath} finished.");
    }

    /// <summary>
    /// Loads references project references
    /// both inside and outside of the solution
    ///
    /// A reference to a project may not
    /// be a project within the solution
    ///
    /// Projects may make references to other projects
    /// that exist in another solution, therefore
    /// We need to do a lookup for that project
    /// that is atypical
    /// </summary>
    /// <param name="solution">Solution (.sln)</param>
    /// <param name="project">Project (.csproj)</param>
    /// <returns>List of the absolute paths of the references listed for the project (.csproj)</returns>
    private List<string>? GetProjectReferences(Solution solution, Project project)
    {
        List<string>? projectReferences = new();
        if (project.AllProjectReferences.Any())
            // project can reference projects outside of the solution
            foreach (var projectReference in project.AllProjectReferences)
            {
                // if we can get the project from the current solution
                // this is a project reference that is within the solution projects
                var referencedProject = solution.GetProject(projectReference.ProjectId);
                if (referencedProject?.FilePath != null)
                {
                    if (!projectReferences.Contains(referencedProject.FilePath))
                        projectReferences.Add(referencedProject.FilePath);
                    else
                        projectModel.Logger.Information(
                            $"Skipping project {referencedProject}. Already in project references.");
                }
                // this is the lookup for project references
                // that occur outside of the solution
                else
                {
                    // path is located in debug name of the project ID
                    try
                    {
                        var projectRefId = projectReference.ProjectId.ToString();

                        // regex split this one
                        // Define the regex pattern to capture the filepath
                        var pattern = @"-\s+(.+\.csproj)";

                        // Match the pattern using regex
                        var match = Regex.Match(projectRefId, pattern);

                        if (match.Success)
                        {
                            // Get the captured group
                            var filepath = match.Groups[1].Value;

                            string[] parts = filepath.Split(['\\', '/']);

                            // Check if the filepath has enough parts to trim
                            if (parts.Length > 2)
                            {
                                // Join the last two parts to form the trimmed filepath
                                var trimmedPath = string.Join("\\", parts[2..]);

                                projectModel.Logger.Information(
                                    $"Original outside project reference filepath {filepath}");
                                projectModel.Logger.Information(
                                    $"Trimmed outside project reference filepath {trimmedPath}");

                                var fullRefPath = Path.Combine(projectModel.DirectoryPath, trimmedPath);

                                if (File.Exists(fullRefPath) && !projectReferences.Contains(fullRefPath))
                                    projectReferences.Add(fullRefPath);
                            }
                            else
                            {
                                projectModel.Logger.Warning(
                                    $"Cannot trim the first two directories from filepath: {filepath}");
                            }
                        }
                        else
                        {
                            projectModel.Logger.Error("No filepath found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        projectModel.Logger.Error($"Couldn't extract project reference {projectReference.ProjectId}");
                    }

                    projectModel.Logger.Information($"Project {referencedProject} filepath is null.");
                }
            }

        return projectReferences;
    }

    /// <summary>
    /// Loads the documents (.cs) for the project
    /// </summary>
    /// <param name="project"></param>
    /// <returns>List of absolute paths to the documents in the project</returns>
    private List<string> GetDocuments(Project project)
    {
        List<string> documents = new();
        var docs = project.Documents.Where(doc => doc.FilePath != null).Select(doc => doc.FilePath).ToList();

        documents.AddRange(docs!);

        return documents;
    }

    /// <summary>
    /// Loads the syntax trees for the project
    /// using a list of documents contained
    /// in the project
    /// </summary>
    /// <param name="project"></param>
    /// <param name="documents"></param>
    /// <returns>Dictionary, key is the absolute path to the document, value is the SyntaxTree</returns>
    private async Task<Dictionary<string, SyntaxTree>?> GetSyntaxTrees(Project project, List<string> documents)
    {
        projectModel.Logger.Information($"Creating project {project.FilePath} syntax trees.");
        Dictionary<string, SyntaxTree>? treeDict = new();
        try
        {
            foreach (var document in documents)
            {
                // parses the syntax tree, necessary for both syntax analysis but also
                // for the semantic analysis using CSharpCompilation
                var syntaxTree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(document), path: document);
                if (treeDict.TryAdd(document, syntaxTree))
                    projectModel.Logger.Information($"Added {document} syntax tree.");
                else
                    projectModel.Logger.Information($"Skipping {document} syntax tree. Already exists.");
            }
        }
        catch (Exception ex)
        {
            projectModel.Logger.Error(ex.Message);
        }

        return treeDict;
    }
}