using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Database;
using TestMap.Services.Database.Repositories;

namespace TestMap.Services.Database;

public class SqliteDatabaseService : ISqliteDatabaseService
{
    private readonly ProjectModel _projectModel;
    private readonly string _dbPath;

    public ProjectRepository ProjectRepository { get; set; }

    public AnalysisSolutionRepository AnalysisSolutionRepository { get; set; }

    public AnalysisProjectRepository AnalysisProjectRepository { get; set; }

    public SourceFileRepository SourceFileRepository { get; set; }

    public ImportRepository ImportRepository { get; set; }

    public ClassRepository ClassRepository { get; set; }

    public MethodRepository MethodRepository { get; set; }

    public InvocationRepository InvocationRepository { get; set; }

    public PropertyRepository PropertyRepository { get; set; }

    public CoverageReportRepository CoverageReportRepository { get; set; }

    public PackageCoverageRepository PackageCoverageRepository { get; set; }

    public ClassCoverageRepository ClassCoverageRepository { get; set; }

    public MethodCoverageRepository MethodCoverageRepository { get; set; }

    public TestResultRepository TestResultRepository { get; set; }

    public TestRunRepository TestRunRepository { get; set; }

    public GeneratedTestRepository GeneratedTestRepository { get; set; }
    
    public FileMutationResultRepository FileMutationResultRepository { get; set; }
    public MutationReportRepository MutationReportRepository { get; set; }
    
    public MutantTestMapRepository MutantTestMapRepository { get; set; }
    public MutantRepository MutantRepository { get; set; }
    
    public LizardFileCodeMetricsRepository LizardFileCodeMetricsRepository { get; set; }
    public LizardFunctionCodeMetricsRepository LizardFunctionCodeMetricsRepository { get; set; }

    public SqliteDatabaseService(ProjectModel projectModel)
    {
        _projectModel = projectModel;

        // Set DB path under the project output directory, e.g. output/<runDate>/<projectId>/project.db
        var dbFolder = Path.Combine(_projectModel.OutputPath ?? string.Empty);
        if (!Directory.Exists(dbFolder)) Directory.CreateDirectory(dbFolder);

        _dbPath = Path.Combine(dbFolder, "analysis.db");

        ProjectRepository = new ProjectRepository(projectModel, _dbPath);
        AnalysisSolutionRepository = new AnalysisSolutionRepository(projectModel, _dbPath);
        AnalysisProjectRepository = new AnalysisProjectRepository(projectModel, _dbPath);
        SourceFileRepository = new SourceFileRepository(projectModel, _dbPath);
        ImportRepository = new ImportRepository(projectModel, _dbPath);
        ClassRepository = new ClassRepository(projectModel, _dbPath);
        MethodRepository = new MethodRepository(projectModel, _dbPath);
        InvocationRepository = new InvocationRepository(projectModel, _dbPath);
        PropertyRepository = new PropertyRepository(projectModel, _dbPath);
        // Coverage
        CoverageReportRepository = new CoverageReportRepository(projectModel, _dbPath);
        PackageCoverageRepository = new PackageCoverageRepository(projectModel, _dbPath);
        ClassCoverageRepository = new ClassCoverageRepository(projectModel, _dbPath);
        MethodCoverageRepository = new MethodCoverageRepository(projectModel, _dbPath);
        
        // test results
        TestResultRepository = new TestResultRepository(projectModel, _dbPath);
        TestRunRepository = new TestRunRepository(projectModel, _dbPath);
        
        // generated tests
        GeneratedTestRepository = new GeneratedTestRepository(projectModel, _dbPath);
        
        // Mutation
        FileMutationResultRepository = new FileMutationResultRepository(projectModel, _dbPath);
        MutationReportRepository = new MutationReportRepository(projectModel, _dbPath);
        MutantTestMapRepository = new MutantTestMapRepository(projectModel, _dbPath);
        MutantRepository = new MutantRepository(projectModel, _dbPath);
        
        // Metrics
        LizardFileCodeMetricsRepository = new LizardFileCodeMetricsRepository(projectModel, _dbPath);
        LizardFunctionCodeMetricsRepository = new LizardFunctionCodeMetricsRepository(projectModel, _dbPath);

    }

    /// <summary>
    /// Initialize DB - creates file if needed and runs migration scripts.
    /// </summary>
    public async Task InitializeAsync()
    {
        _projectModel.Logger?.Information($"Initializing SQLite DB at {_dbPath}");
        try
        {
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync();

            await using var cmd = new SqliteCommand(Migrations.Schema, conn);
            await cmd.ExecuteNonQueryAsync();

            _projectModel.Logger?.Information("SQLite migrations executed successfully.");
        }
        catch (Exception ex)
        {
            _projectModel.Logger?.Error($"Error executing migrations: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Opens and returns a SQLiteConnection
    /// Caller is responsible for disposing the connection
    /// </summary>
    public async Task<SqliteConnection> GetOpenConnectionAsync()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        return conn;
    }

    public async Task InsertProjectInformation()
    {
        await ProjectRepository.InsertProjectModelGetId();
        ApplyProjectModelIdToSolutions();
        await AnalysisSolutionRepository.InsertAnalysisSolutionGetId();
        ApplySolutionIdToProjects();
        await AnalysisProjectRepository.InsertAnalysisProjectGetId();
    }


    public void ApplyProjectModelIdToSolutions()
    {
        foreach (var solution in _projectModel.Solutions) solution.ProjectModelId = _projectModel.DbId;
    }


    public void ApplySolutionIdToProjects()
    {
        foreach (var project in _projectModel.Projects)
            project.SolutionId =
                _projectModel.Solutions.Find(x => x.SolutionFilePath == project.SolutionFilePath)?.Id ?? 0;
    }


    public async Task<List<CoverageMethodResult>> FindMethodsWithLowCoverage()
    {
        var results = new List<CoverageMethodResult>();

        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            select * from v_baseline_uncovered_tested_methods;
        ";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var item = new CoverageMethodResult
            {
                MethodId = reader.GetInt32(0),
                MethodName = reader.GetString(1),
                MethodBody = reader.GetString(2),
                LineRate = reader.GetDouble(3),
                BranchRate = reader.GetDouble(4),

                ClassId = reader.GetInt32(5),
                ClassName = reader.GetString(6),

                CoverageStatus = reader.GetString(7),

                TestMethodId = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                TestMethodName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                TestMethodBody = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                TestClassId = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                TestClassName = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                TestFramework = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),

                TestClassLineStart = reader.IsDBNull(14) ? 0 : reader.GetInt32(14),
                TestClassBodyStart = reader.IsDBNull(15) ? 0 : reader.GetInt32(15),
                TestClassLineEnd = reader.IsDBNull(16) ? 0 : reader.GetInt32(16),
                TestClassBodyEnd = reader.IsDBNull(17) ? 0 : reader.GetInt32(17),

                TestFilePath = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                TestDependencies = reader.IsDBNull(19) ? string.Empty : reader.GetString(19),

                SolutionFilePath = reader.IsDBNull(20) ? string.Empty : reader.GetString(20)
            };

            results.Add(item);
        }

        return results;
    }
}