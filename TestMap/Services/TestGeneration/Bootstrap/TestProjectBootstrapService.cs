using System.Diagnostics;
using System.Xml.Linq;
using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Bootstrap;

public sealed class TestProjectBootstrapService : ITestProjectBootstrapService
{
    public async Task<TestProjectBootstrapResult> CreateTestProjectAsync(
        TestBootstrapRequest request,
        bool applyChanges = false,
        CancellationToken cancellationToken = default)
    {
        var sourceProjectName = Path.GetFileNameWithoutExtension(request.SourceProjectPath);
        var sourceProjectDirectory = Path.GetDirectoryName(request.SourceProjectPath) ?? string.Empty;
        var parentDirectory = Directory.GetParent(sourceProjectDirectory)?.FullName ?? sourceProjectDirectory;
        var testProjectName = $"{sourceProjectName}{request.TestProjectSuffix}";
        var testProjectDirectory = Path.Combine(parentDirectory, testProjectName);
        var testProjectPath = Path.Combine(testProjectDirectory, $"{testProjectName}.csproj");
        var packageReferences = BuildPackageReferences(request.Framework, request.AddCoverletCollector);
        var targetFramework = ResolveTargetFramework(request.SourceProjectPath);
        var plannedOperations = new List<string>
        {
            $"Create test project directory '{testProjectDirectory}'",
            $"Create test project file '{testProjectPath}'",
            $"Add project reference to '{request.SourceProjectPath}'",
            request.SolutionPath == null
                ? "No solution path provided; solution integration is skipped."
                : $"Add test project to solution '{request.SolutionPath}'"
        };

        if (applyChanges)
        {
            Directory.CreateDirectory(testProjectDirectory);

            if (!File.Exists(testProjectPath))
            {
                var projectDocument = CreateProjectDocument(
                    testProjectPath,
                    request.SourceProjectPath,
                    request.Framework,
                    targetFramework,
                    packageReferences);
                await File.WriteAllTextAsync(testProjectPath, projectDocument.ToString(), cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(request.SolutionPath) && File.Exists(request.SolutionPath))
                await EnsureProjectAddedToSolutionAsync(request.SolutionPath, testProjectPath, cancellationToken);
        }

        return new TestProjectBootstrapResult
        {
            Success = true,
            TestProjectName = testProjectName,
            TestProjectPath = testProjectPath,
            SolutionPath = request.SolutionPath,
            PackageReferences = packageReferences,
            PlannedOperations = plannedOperations
        };
    }

    private static XDocument CreateProjectDocument(
        string testProjectPath,
        string sourceProjectPath,
        string framework,
        string targetFramework,
        IReadOnlyList<string> packageReferences)
    {
        var packageItems = packageReferences.Select(package =>
        {
            var element = new XElement(
                "PackageReference",
                new XAttribute("Include", package),
                new XAttribute("Version", ResolvePackageVersion(package)));

            if (string.Equals(package, "coverlet.collector", StringComparison.OrdinalIgnoreCase))
            {
                element.Add(new XElement("PrivateAssets", "all"));
                element.Add(new XElement("IncludeAssets",
                    "runtime; build; native; contentfiles; analyzers; buildtransitive"));
            }

            return element;
        });

        var projectReferencePath = Path.GetRelativePath(
            Path.GetDirectoryName(testProjectPath) ?? string.Empty,
            sourceProjectPath);

        return new XDocument(
            new XElement("Project",
                new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup",
                    new XElement("TargetFramework", targetFramework),
                    new XElement("ImplicitUsings", "enable"),
                    new XElement("Nullable", "enable"),
                    new XElement("IsPackable", "false"),
                    new XElement("IsTestProject", "true")),
                new XElement("ItemGroup", packageItems),
                new XElement("ItemGroup",
                    new XElement("ProjectReference", new XAttribute("Include", projectReferencePath))),
                CreateFrameworkSpecificPropertyGroup(framework)));
    }

    private static XElement? CreateFrameworkSpecificPropertyGroup(string framework)
    {
        return framework switch
        {
            "MSTest" => new XElement("PropertyGroup", new XElement("RootNamespace", "Tests")),
            _ => null
        };
    }

    private static string ResolveTargetFramework(string sourceProjectPath)
    {
        try
        {
            var document = XDocument.Load(sourceProjectPath);
            var targetFramework = document.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "TargetFramework")?.Value;
            if (!string.IsNullOrWhiteSpace(targetFramework)) return targetFramework.Trim();

            var targetFrameworks = document.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "TargetFrameworks")?.Value;
            if (!string.IsNullOrWhiteSpace(targetFrameworks))
                return targetFrameworks
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault() ?? "net10.0";
        }
        catch
        {
            // Fall through to default below.
        }

        return "net10.0";
    }

    private static IReadOnlyList<string> BuildPackageReferences(string framework, bool addCoverletCollector)
    {
        var packages = framework switch
        {
            "NUnit" => new List<string> { "NUnit", "NUnit3TestAdapter", "Microsoft.NET.Test.Sdk" },
            "MSTest" => new List<string> { "MSTest.TestFramework", "MSTest.TestAdapter", "Microsoft.NET.Test.Sdk" },
            _ => new List<string> { "xunit", "xunit.runner.visualstudio", "Microsoft.NET.Test.Sdk" }
        };

        if (addCoverletCollector) packages.Add("coverlet.collector");

        return packages;
    }

    private static string ResolvePackageVersion(string packageName)
    {
        return packageName switch
        {
            "Microsoft.NET.Test.Sdk" => "17.12.0",
            "xunit" => "2.9.2",
            "xunit.runner.visualstudio" => "2.8.2",
            "NUnit" => "4.2.2",
            "NUnit3TestAdapter" => "4.6.0",
            "MSTest.TestFramework" => "3.6.4",
            "MSTest.TestAdapter" => "3.6.4",
            "coverlet.collector" => "6.0.2",
            _ => "1.0.0"
        };
    }

    private static async Task EnsureProjectAddedToSolutionAsync(
        string solutionPath,
        string testProjectPath,
        CancellationToken cancellationToken)
    {
        var solutionText = await File.ReadAllTextAsync(solutionPath, cancellationToken);
        if (solutionText.Contains(Path.GetFileName(testProjectPath), StringComparison.OrdinalIgnoreCase)) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"sln \"{solutionPath}\" add \"{testProjectPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory
        };

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("Failed to start dotnet sln add process.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to add test project to solution '{solutionPath}': {stderr}");
        }
    }
}