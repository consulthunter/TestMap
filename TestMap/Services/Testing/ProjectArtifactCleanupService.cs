using TestMap.App;

namespace TestMap.Services.Testing;

public class ProjectArtifactCleanupService
{
    private readonly ProjectContext _context;

    public ProjectArtifactCleanupService(ProjectContext context)
    {
        _context = context;
    }

    public void CleanupProjectDirectory(bool preserveArtifacts)
    {
        if (preserveArtifacts)
        {
            _context.Project.Logger?.Information("Preserving coverage and mutation directories for failed run diagnostics.");
            return;
        }

        var coverageDir = Path.Combine(_context.Project.DirectoryPath!, "coverage");
        var mutationDir = Path.Combine(_context.Project.DirectoryPath!, "mutation");

        DeleteDirectoryIfPresent(coverageDir);
        DeleteDirectoryIfPresent(mutationDir);
        DeleteGeneratedTestResultDirectories();
    }

    private void DeleteDirectoryIfPresent(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
            _context.Project.Logger?.Information("Directory '{Path}' deleted successfully.", path);
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Warning("Failed to delete directory '{Path}': {Message}", path, ex.Message);
        }
    }

    private void DeleteGeneratedTestResultDirectories()
    {
        var projectDirectory = _context.Project.DirectoryPath;
        if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
        {
            return;
        }

        try
        {
            foreach (var path in Directory.EnumerateDirectories(projectDirectory, "TestResults", SearchOption.AllDirectories))
            {
                DeleteDirectoryIfPresent(path);
            }
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Warning(
                "Failed to enumerate generated TestResults directories under '{Path}': {Message}",
                projectDirectory,
                ex.Message);
        }
    }
}
