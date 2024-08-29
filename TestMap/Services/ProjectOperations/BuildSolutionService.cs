﻿using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class BuildSolutionService
{
    private ProjectModel _projectModel;

    public BuildSolutionService(ProjectModel projectModel)
    {
        _projectModel = projectModel;
    }

    public virtual async Task BuildSolutionsAsync()
    {
        await FindAllSolutionFilesAsync();
    }
    private async Task FindAllSolutionFilesAsync()
    {
        var solutions = Directory.GetFiles(_projectModel.DirectoryPath, "*.sln", SearchOption.AllDirectories);

        // we work through all of the solutions
        // then do the projects outside the loop
        foreach (var solution in solutions)
        {
            _projectModel.Logger.Information($"Solution file found: {solution}");
            await BuildSolutionAsync(solution);
            await LoadSolutionAsync(solution);
        }
    }
    private async Task BuildSolutionAsync(string solutionPath)
    {
        // dotnet restore / build
        string configuration = "Debug"; // or "Debug"

        var scriptClean = $"dotnet clean {solutionPath}";    
        var scriptRestore = $"dotnet restore {solutionPath}";
        var scriptBuild = $"dotnet build --configuration {configuration} {solutionPath}";

        PowerShellRunner runner = new PowerShellRunner();
        _projectModel.Logger.Information($"Building solution {solutionPath}");
        await runner.RunScript([scriptClean, scriptRestore, scriptBuild]);
        _projectModel.Logger.Information($"Building {solutionPath} finished.");

        if (runner.Error)
        {
            foreach (var e in runner.Errors)
            {
                _projectModel.Logger.Error(e);
            }
        }
    }
    private async Task LoadSolutionAsync(string solutionPath)
    {
        try
        {
            _projectModel.Logger.Information($"Loading solution {solutionPath}");
            using (var workspace = MSBuildWorkspace.Create())
            {
                var solution = await workspace.OpenSolutionAsync(solutionPath);

                var projects = solution.Projects.ToList();

                List<string> solutionProjects = new();

                foreach (var project in projects)
                {
                    List<string> documents = GetDocuments(project);
                    List<MetadataReference> assemblies = GetAssemblies(project);
                    List<string> projectReferences = GetProjectReferences(solution, project);
                    Dictionary<string, SyntaxTree> syntaxTrees = await GetSyntaxTrees(project, documents);

                    if (project.FilePath != null)
                    {
                        var filepath = project.FilePath;
                        
                        // Add filepath and project references
                        AnalysisProject analysisProject = new AnalysisProject(syntaxTrees: syntaxTrees, projectReferences: projectReferences, 
                            assemblies: assemblies, documents: documents, filepath);

                        if (!_projectModel.Projects.Exists(proj => proj.ProjectFilePath == filepath) && !solutionProjects.Exists(path => path == filepath))
                        {
                            _projectModel.Projects.Add(analysisProject);
                            solutionProjects.Add(filepath);
                        }
                        else
                        {
                            _projectModel.Logger.Warning($"Project {project.Id} filepath already exists in the list.");
                        }
                    }
                    else
                    {
                        // If filepath already exists in addedProjects, skip adding
                        _projectModel.Logger.Warning($"Project {project.Id} filepath is null.");
                    }
                }

                if (!_projectModel.Solutions.Exists(sol => sol.Solution.FilePath == solution.FilePath))
                {
                    AnalysisSolution analysisSolution = new AnalysisSolution(solution, solutionProjects);
                    _projectModel.Solutions.Add(analysisSolution);
                }
                else
                {
                    _projectModel.Logger.Warning($"Solution {solution.FilePath}already exists in the list.");
                }
                
            }
        }
        catch (Exception ex)
        {
            _projectModel.Logger.Error(ex.Message);
        }
        _projectModel.Logger.Information($"Loading {solutionPath} finished.");
    }

    private List<string> GetProjectReferences(Solution solution, Project project)
    {
        List<string> projectReferences = new();
        if (project.AllProjectReferences.Any())
        {
            // project can reference projects outside of the solution
            foreach (var projectReference in project.AllProjectReferences)
            {
                var referencedProject = solution.GetProject(projectReference.ProjectId);
                if (referencedProject?.FilePath != null)
                {
                    if (!projectReferences.Contains(referencedProject.FilePath))
                    {
                        projectReferences.Add(referencedProject.FilePath);
                    }
                    else
                    {
                        _projectModel.Logger.Information($"Skipping project {referencedProject}. Already in project references.");
                    }
                }
                else
                {
                    // path is located in debug name of the project ID
                    try
                    {
                        string projectRefId = projectReference.ProjectId.ToString();
                        // regex split this one
                        
                        // Define the regex pattern to capture the filepath
                        string pattern = @"-\s+(.+\.csproj)";

                        // Match the pattern using regex
                        Match match = Regex.Match(projectRefId, pattern);
                        
                        if (match.Success)
                        {
                            // Get the captured group
                            string filepath = match.Groups[1].Value;
                            
                            string[] parts = filepath.Split(['\\', '/']);

                            // Check if the filepath has enough parts to trim
                            if (parts.Length > 2)
                            {
                                // Join the last two parts to form the trimmed filepath
                                string trimmedPath = string.Join("\\", parts[2..]);
                                
                                _projectModel.Logger.Information($"Original outside project reference filepath {filepath}");
                                _projectModel.Logger.Information($"Trimmed outside project reference filepath {trimmedPath}");

                                string fullRefPath = Path.Combine(_projectModel.DirectoryPath, trimmedPath);

                                if (File.Exists(fullRefPath) && !projectReferences.Contains(fullRefPath))
                                {
                                    projectReferences.Add(fullRefPath);
                                }
                            }
                            else
                            {
                                _projectModel.Logger.Warning($"Cannot trim the first two directories from filepath: {filepath}");
                            }
                        }
                        else
                        {
                            _projectModel.Logger.Error("No filepath found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _projectModel.Logger.Error($"Couldn't extract project reference {projectReference.ProjectId}");
                    }

                    _projectModel.Logger.Information($"Project {referencedProject} filepath is null.");
                }
            }
        }

        return projectReferences;
    }

    private List<MetadataReference> GetAssemblies(Project project)
    {
        List<MetadataReference> assemblies = new();
        
        var references = project.MetadataReferences.ToList();
        
        assemblies.AddRange(references);

        return assemblies;
    }

    private List<string> GetDocuments(Project project)
    {
        List<string> documents = new();
        var docs = project.Documents.Where(doc => doc.FilePath != null).Select(doc => doc.FilePath).ToList();
        
        documents.AddRange(docs!);

        return documents;
    }

    private async Task<Dictionary<string, SyntaxTree>> GetSyntaxTrees(Project project, List<string> documents)
    {
        _projectModel.Logger.Information($"Creating project {project.FilePath} syntax trees.");
        Dictionary<string, SyntaxTree> treeDict = new();
        try
        {
            foreach (var document in documents)
            {
                // parses the syntax tree, necessary for both syntax analysis but also
                // for the semantic analysis using CSharpCompilation
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(text: await File.ReadAllTextAsync(document), path: document);
                if (treeDict.TryAdd(document, syntaxTree))
                {
                    _projectModel.Logger.Information($"Added {document} syntax tree.");
                }
                else
                {
                    _projectModel.Logger.Information($"Skipping {document} syntax tree. Already exists.");
                }
            }
        }
        catch (Exception ex)
        {
            _projectModel.Logger.Error(ex.Message);
        }

        return treeDict;
    }
}