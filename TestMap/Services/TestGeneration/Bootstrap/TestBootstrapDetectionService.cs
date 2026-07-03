using TestMap.App;
using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Bootstrap;

public sealed class TestBootstrapDetectionService : ITestBootstrapDetectionService
{
    private readonly ProjectContext _context;

    public TestBootstrapDetectionService(ProjectContext context)
    {
        _context = context;
    }

    public Task<TestBootstrapDetectionResult> DetectAsync(CancellationToken cancellationToken = default)
    {
        var hasTestProjects = _context.Project.Projects.Any(x => x.BuildMetadata.IsTestProject);
        var hasDiscoveredTestMembers = _context.Project.Projects.Any(x =>
            x.DocumentFilePaths.Any(path =>
                path.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("Tests", StringComparison.OrdinalIgnoreCase)));

        var shouldBootstrap = !hasTestProjects && !hasDiscoveredTestMembers;
        var reason = shouldBootstrap
            ? "No test projects or discovered test members were found."
            : "Existing test infrastructure was detected.";

        return Task.FromResult(new TestBootstrapDetectionResult
        {
            ShouldBootstrap = shouldBootstrap,
            Reason = reason,
            HasTestProjects = hasTestProjects,
            HasDiscoveredTestMembers = hasDiscoveredTestMembers
        });
    }
}