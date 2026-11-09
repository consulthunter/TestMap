using System.Text.RegularExpressions;
using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery.Contracts;

namespace TestMap.Services.ProjectDiscovery;

public sealed partial class ProjectTestPresencePolicy : IProjectTestPresencePolicy
{
    private static readonly HashSet<string> ProjectExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".csproj", ".fsproj", ".vbproj" };
    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".fs", ".vb" };

    public string Name => ProjectCheckPolicy.Name;
    public string Version => ProjectCheckPolicy.Version;

    public ProjectCheckEvidence? FindEvidence(IEnumerable<string> paths)
    {
        foreach (var path in paths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Select(NormalizePath)
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => segment.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                                        segment.Equals("tests", StringComparison.OrdinalIgnoreCase)))
                return new ProjectCheckEvidence(ProjectCheckEvidenceCategory.TestDirectory, path);

            var fileName = segments.LastOrDefault() ?? string.Empty;
            var extension = Path.GetExtension(fileName);
            var stem = Path.GetFileNameWithoutExtension(fileName);
            if (ProjectExtensions.Contains(extension) && BoundedTestToken().IsMatch(stem))
                return new ProjectCheckEvidence(ProjectCheckEvidenceCategory.TestProjectFile, path);
            if (SourceExtensions.Contains(extension) && IsTestSourceStem(stem))
                return new ProjectCheckEvidence(ProjectCheckEvidenceCategory.TestSourceFile, path);
        }

        return null;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').Trim('/');

    private static bool IsTestSourceStem(string stem) =>
        TestSourceSeparatedSuffix().IsMatch(stem) ||
        stem.EndsWith("Test", StringComparison.Ordinal) ||
        stem.EndsWith("Tests", StringComparison.Ordinal);

    [GeneratedRegex(@"(?:^|[._\-\s])tests?(?:$|[._\-\s])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BoundedTestToken();

    [GeneratedRegex(@"(?:^|[._\-\s])tests?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TestSourceSeparatedSuffix();
}
