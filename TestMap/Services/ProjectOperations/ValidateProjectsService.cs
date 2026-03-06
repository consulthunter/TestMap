using TestMap.Models;
using TestMap.Models.Results;
using TestMap.App;
using TestMap.Services.Database;

namespace TestMap.Services.ProjectOperations;

public class ValidateProjectsService : IValidateProjectsService
{
    private ProjectContext _context;
    private SqliteDatabaseService _sqliteDatabaseService;

    public ValidateProjectsService(ProjectContext context, SqliteDatabaseService sqliteDatabaseService)
    {
        _context = context;
        _sqliteDatabaseService = sqliteDatabaseService;
    }
    
    public async Task ValidateProjectAsync()
    {
        // check the db for coverage report, mutation report, etc.
        var hasTests = await _sqliteDatabaseService.CoverageReportRepository.HasCoverageReports();
        var hasPassingTests = await _sqliteDatabaseService.TestResultRepository.HasPassingTests();
        var hasCandidateMethods = await _sqliteDatabaseService.HasCandidateMethods();
        // need to look to see if the project has xunit or not
        var hasXunit = await _sqliteDatabaseService.MethodRepository.HasXUnitTestMethods();
        var hasMutationReports = await _sqliteDatabaseService.MutationReportRepository.HasMutationReports();
        var hasFileCodeMetrics =
            await _sqliteDatabaseService.LizardFileCodeMetricsRepository.HasLizardFileCodeMetrics();
        var hasFunctionCodeMetrics =
            await _sqliteDatabaseService.LizardFunctionCodeMetricsRepository.HasLizardFunctionCodeMetrics();
        
        var result = new ProjectValidationResult(
            _context.Project.GitHubUrl,
            _context.Project.Owner,
            _context.Project.RepoName,
            hasXunit,
            hasTests,
            hasPassingTests,
            hasCandidateMethods,
            hasMutationReports,
            hasFileCodeMetrics,
            hasFunctionCodeMetrics
        );

        WriteCsvRow(result);
        
    }
    
    private void WriteCsvRow(ProjectValidationResult result)
    {
        // Parent of the project output directory
        var outputRoot = Directory.GetParent(_context.Project.OutputPath)!.FullName;
        var csvPath = Path.Combine(outputRoot, "project-validation.csv");

        var fileExists = File.Exists(csvPath);

        using var writer = new StreamWriter(csvPath, append: true);

        // Write header once
        if (!fileExists)
        {
            writer.WriteLine(
                "URL,Owner,Repo,HasXUnit,HasCoverage,HasPassingTests,HasCandidateMethods,HasMutationReports,HasFileCodeMetrics,HasFunctionCodeMetrics");
        }

        writer.WriteLine(
            $"{result.Url}," +
            $"{result.Owner}," +
            $"{result.Repo}," +
            $"{result.HasXUnit}," +
            $"{result.HasCoverage}," +
            $"{result.HasPassingTests}," +
            $"{result.HasCandidateMethods}," +
            $"{result.HasMutationReports}," +
            $"{result.HasFileCodeMetrics}," +
            $"{result.HasFunctionCodeMetrics}");
    }
}