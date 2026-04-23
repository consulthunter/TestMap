using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Results;
using TestMap.Persistence.Ef;

namespace TestMap.Services.CollectInformation;

public class CollectTestsResultWriter
{
    private readonly ProjectContext _context;
    private readonly TestMapDbContext _dbContext;

    public CollectTestsResultWriter(ProjectContext context, TestMapDbContext dbContext)
    {
        _context = context;
        _dbContext = dbContext;
    }

    public async Task WriteAsync(CancellationToken cancellationToken = default)
    {
        var projectId = _context.Project.DbId;
        var latestRun = await _dbContext.TestRuns
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var result = new ProjectValidationResult(
            _context.Project.GitHubUrl,
            _context.Project.Owner,
            _context.Project.RepoName,
            HasXUnit(),
            await _dbContext.CoverageReports.AnyAsync(x => x.ProjectId == projectId, cancellationToken),
            latestRun?.Success == true,
            await _dbContext.CandidateMethods.AnyAsync(cancellationToken),
            await _dbContext.MutationTestingReports.AnyAsync(x => x.ProjectId == projectId, cancellationToken));

        await WriteCsvRowAsync(result, cancellationToken);
    }

    private bool HasXUnit()
    {
        return _context.Project.Projects.Any(project => project.BuildMetadata.IsTestProject);
    }

    private async Task WriteCsvRowAsync(ProjectValidationResult result, CancellationToken cancellationToken)
    {
        var outputRoot = Directory.GetParent(_context.Project.OutputPath ?? string.Empty)?.FullName
            ?? _context.Project.OutputPath
            ?? _context.Project.DirectoryPath;
        var csvPath = Path.Combine(outputRoot, "project-validation.csv");

        var fileExists = File.Exists(csvPath);
        await using var writer = new StreamWriter(csvPath, append: true);

        if (!fileExists)
        {
            await writer.WriteLineAsync(
                "URL,Owner,Repo,HasXUnit,HasCoverage,HasPassingTests,HasCandidateMethods,HasMutationReports".AsMemory(),
                cancellationToken);
        }

        await writer.WriteLineAsync(
            $"{result.Url},{result.Owner},{result.Repo},{result.HasXUnit},{result.HasCoverage},{result.HasPassingTests},{result.HasCandidateMethods},{result.HasMutationReports}".AsMemory(),
            cancellationToken);
    }
}
