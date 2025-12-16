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

    public SourcePackageRepository SourcePackageRepository { get; set; }

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
        SourcePackageRepository = new SourcePackageRepository(projectModel, _dbPath);
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
            WITH UncoveredMethods AS (
                SELECT 
                    mc.method_id, 
                    mc.line_rate, 
                    m.class_id, 
                    m.name AS method_name,
                    m.full_string AS method_body,
                    c.name AS class_name,
                    sf.id AS source_file_id
                FROM method_coverage mc
                JOIN methods m ON mc.method_id = m.id
                JOIN classes c ON m.class_id = c.id
                JOIN source_files sf ON c.file_id = sf.id
                WHERE mc.line_rate != 1
            ),
            ClassTests AS (
                SELECT 
                    m.class_id,
                    tc.id AS test_class_id,
                    tc.name AS test_class_name,
                    tc.testing_framework,
                    tc.location_start_lin_no AS test_class_lin_start,
                    tc.location_body_start AS test_class_body_start,
                    tc.location_end_lin_no AS test_class_lin_end,
                    tc.location_body_end AS test_class_body_end,
                    tf.path AS test_file_path,
                    tf.usings AS test_dependencies,
                    tm.name AS test_method_name,
                    tm.full_string AS test_method,
                    ROW_NUMBER() OVER(PARTITION BY m.class_id ORDER BY tm.name) AS rn
                FROM methods m
                JOIN invocations ic ON ic.source_method_id = m.id
                JOIN methods tm ON ic.target_method_id = tm.id
                JOIN classes tc ON tm.class_id = tc.id
                JOIN source_files tf ON tc.file_id = tf.id
                WHERE tm.is_test_method = 1
            )
            SELECT 
                um.method_id,
                um.method_name,
                um.method_body,
                um.line_rate,
                um.class_id,
                um.class_name,
                CASE WHEN ct.test_class_id IS NOT NULL THEN 'Has tests in class' ELSE 'No tests in class' END AS coverage_status,
                ct.test_class_id,
                ct.test_class_name,
                ct.testing_framework,
                ct.test_class_lin_start,
                ct.test_class_body_start,
                ct.test_class_lin_end,
                ct.test_class_body_end,
                ct.test_file_path,
                ct.test_dependencies,
                ct.test_method_name,
                ct.test_method,
                asol.solution_path AS solution_file_path
            FROM UncoveredMethods um
            LEFT JOIN ClassTests ct 
                ON um.class_id = ct.class_id
                AND ct.rn = 1
            LEFT JOIN source_packages sp ON sp.id = (SELECT package_id FROM source_files WHERE id = um.source_file_id)
            LEFT JOIN analysis_projects ap ON ap.id = sp.analysis_project_id
            LEFT JOIN analysis_solutions asol ON asol.id = ap.solution_id;
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

                ClassId = reader.GetInt32(4),
                ClassName = reader.GetString(5),

                CoverageStatus = reader.GetString(6),

                TestClassId = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                TestClassName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                TestFramework = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),

                TestClassLineStart = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                TestClassBodyStart = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                TestClassLineEnd = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                TestClassBodyEnd = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),

                TestFilePath = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                TestDependencies = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),

                TestMethodName = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                TestMethodBody = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),

                SolutionFilePath = reader.IsDBNull(18) ? string.Empty : reader.GetString(18)
            };

            results.Add(item);
        }

        return results;
    }
}