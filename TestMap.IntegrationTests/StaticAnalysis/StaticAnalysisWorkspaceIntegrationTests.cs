using Microsoft.Build.Locator;
using TestMap.Services.StaticAnalysis;

namespace TestMap.IntegrationTests.StaticAnalysis;

public sealed class StaticAnalysisWorkspaceIntegrationTests
{
    /// <summary>
    /// Refreshing a project also loads the projects it references. Opening one of those afterwards
    /// must return the loaded project: asking MSBuildWorkspace to load it again throws "is already
    /// part of the workspace" (pilot V6, gridify: code metrics after a tool attempt that added no tests).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task OpenProjectAsync_ProjectLoadedAsReferenceOfRefreshedProject_ReturnsLoadedProject()
    {
        RegisterMSBuild();
        var root = Path.Combine(Path.GetTempPath(), "testmap-workspace-" + Guid.NewGuid().ToString("N"));
        try
        {
            var referenced = WriteProject(root, "Shared.Tests", references: []);
            var referencing = WriteProject(root, "Integration.Tests", references: [referenced]);
            using var workspace = new StaticAnalysisWorkspace();

            var refreshed = await workspace.RefreshProjectAsync(referencing);
            var opened = await workspace.OpenProjectAsync(referenced);

            Assert.Equal("Integration.Tests", refreshed.Name);
            Assert.Equal(Path.GetFullPath(referenced), opened.FilePath, ignoreCase: true);
            Assert.Contains(opened.Documents, document => document.Name == "Shared.TestsType.cs");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void RegisterMSBuild()
    {
        if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();
    }

    private static string WriteProject(string root, string name, IReadOnlyList<string> references)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        var projectReferences = string.Join(
            Environment.NewLine,
            references.Select(path => $"    <ProjectReference Include=\"{path}\" />"));
        var projectPath = Path.Combine(directory, name + ".csproj");
        File.WriteAllText(projectPath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
            {projectReferences}
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(directory, name + "Type.cs"),
            $"namespace {name.Replace('.', '_')}; public class {name.Replace(".", "")}Type {{ }}");
        return projectPath;
    }
}
