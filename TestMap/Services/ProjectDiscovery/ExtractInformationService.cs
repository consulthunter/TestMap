/*
 * consulthunter
 * 2024-11-07
 * Finds and loads solutions
 * and their projects
 * BuildSolutionService.cs
 */

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Services.StaticAnalysis;

namespace TestMap.Services.ProjectDiscovery;

public class ExtractInformationService(
    ProjectContext context,
    IProjectBuildAnalysisService projectBuildAnalysisService,
    IStaticAnalysisWorkspace staticAnalysisWorkspace) : IExtractInformationService
{
    /// <summary>
    ///     Entry point for service
    /// </summary>
    public virtual async Task ExtractInfoAsync()
    {
        await FindAllSolutionFilesAsync();
    }

    /// <summary>
    ///     Does a recursive search looking for
    ///     solution files in the repo
    /// </summary>
    private async Task FindAllSolutionFilesAsync()
    {
        if (!Path.Exists(context.Project.DirectoryPath)) return;

        var solutions = Directory.EnumerateFiles(context.Project.DirectoryPath, "*.sln", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(context.Project.DirectoryPath, "*.slnx", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var solution in solutions)
        {
            context.Project.Logger?.Information($"Solution file found: {solution}");
            await LoadSolutionAsync(solution);
        }
    }

    /// <summary>
    ///     Opens the solution and loads the projects
    ///     associated with the solution
    /// </summary>
    private async Task LoadSolutionAsync(string solutionPath)
    {
        try
        {
            context.Project.Logger?.Information($"Loading solution {solutionPath}");

            var solution = await staticAnalysisWorkspace.OpenSolutionAsync(solutionPath);
            var projects = solution.Projects.ToList();
            List<string> solutionProjects = new();

            foreach (var project in projects)
            {
                var documents = GetDocuments(project);
                var buildMetadata = await projectBuildAnalysisService.AnalyzeAsync(project);

                if (project.FilePath == null)
                {
                    context.Project.Logger?.Warning($"Project {project.Id} filepath is null.");
                    continue;
                }

                var buildTargets = buildMetadata.BuildTargets;
                var analysisProject = new CSharpProjectModel(
                    GetProjectReferences(solution, project),
                    documents,
                    buildTargets,
                    buildMetadata,
                    filePath: project.FilePath,
                    defaultBuildTarget: buildMetadata.DefaultBuildTarget);

                var filepath = project.FilePath;

                if (!context.Project.Projects.Exists(proj => proj.FilePath == filepath) &&
                    !solutionProjects.Exists(path => path == filepath))
                {
                    context.Project.Projects.Add(analysisProject);
                    solutionProjects.Add(filepath);
                    context.Project.Logger?.Information($"Project metadata: {buildMetadata.Notes}");
                }
                else
                {
                    context.Project.Logger?.Warning($"Project {project.Id} filepath already exists in the list.");
                }
            }

            if (!context.Project.Solutions.Exists(sol => sol.FilePath == (solution.FilePath ?? solutionPath)))
            {
                var analysisSolution =
                    new CSharpSolutionModel(solutionProjects, filePath: solution.FilePath ?? solutionPath);
                context.Project.Solutions.Add(analysisSolution);
            }
            else
            {
                context.Project.Logger?.Warning($"Solution {solution.FilePath} already exists in the list.");
            }
        }
        catch (Exception ex)
        {
            context.Project.Logger?.Error(ex, "Failed to load solution {SolutionPath}", solutionPath);
        }

        context.Project.Logger?.Information($"Loading {solutionPath} finished.");
    }

    /// <summary>
    ///     Loads references project references
    ///     both inside and outside of the solution
    /// </summary>
    private List<string> GetProjectReferences(Solution solution, Project project)
    {
        List<string> projectReferences = new();
        if (project.AllProjectReferences.Any())
            foreach (var projectReference in project.AllProjectReferences)
            {
                var referencedProject = solution.GetProject(projectReference.ProjectId);
                if (referencedProject?.FilePath != null)
                {
                    if (!projectReferences.Contains(referencedProject.FilePath))
                        projectReferences.Add(referencedProject.FilePath);
                    else
                        context.Project.Logger?.Information(
                            $"Skipping project {referencedProject}. Already in project references.");
                }
                else
                {
                    try
                    {
                        var projectRefId = projectReference.ProjectId.ToString();
                        var pattern = @"-\s+(.+\.csproj)";
                        var match = Regex.Match(projectRefId, pattern);

                        if (match.Success)
                        {
                            var filepath = match.Groups[1].Value;
                            var parts = filepath.Split('\\', '/');

                            if (parts.Length > 2)
                            {
                                var trimmedPath = string.Join("\\", parts[2..]);
                                var fullRefPath = Path.Combine(context.Project.DirectoryPath, trimmedPath);

                                if (File.Exists(fullRefPath) && !projectReferences.Contains(fullRefPath))
                                    projectReferences.Add(fullRefPath);
                            }
                            else
                            {
                                context.Project.Logger?.Warning(
                                    $"Cannot trim the first two directories from filepath: {filepath}");
                            }
                        }
                        else
                        {
                            context.Project.Logger?.Error("No filepath found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        context.Project.Logger?.Error(
                            $"Couldn't extract project reference {projectReference.ProjectId}: {ex.Message}");
                    }
                }
            }

        return projectReferences;
    }

    private static List<string> GetDocuments(Project project)
    {
        List<string> documents = new();
        var docs = project.Documents.Where(doc => doc.FilePath != null).Select(doc => doc.FilePath).ToList();
        documents.AddRange(docs!);
        return documents;
    }
}