using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Database;
using TestMap.App;
using TestMap.Services.Database.Repositories;

namespace TestMap.Services.Database;

public class SqliteDatabaseService : ISqliteDatabaseService
{
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
    
    public MethodTestSmellRepository MethodTestSmellRepository { get; set; }
    public TestSmellRepository TestSmellRepository { get; set; }

    private readonly ProjectContext _context;

    public SqliteDatabaseService(ProjectContext context)
    {
        _context = context;

        // Set DB path under the project output directory, e.g. output/<runDate>/<projectId>/project.db
        var dbFolder = Path.Combine(_context.Project.OutputPath ?? string.Empty);
        if (!Directory.Exists(dbFolder)) Directory.CreateDirectory(dbFolder);
    
        _dbPath = Path.Combine(dbFolder, "analysis.db");

        ProjectRepository = new ProjectRepository(_context.Project, _dbPath);
        AnalysisSolutionRepository = new AnalysisSolutionRepository(_context.Project, _dbPath);
        AnalysisProjectRepository = new AnalysisProjectRepository(_context.Project, _dbPath);
        SourceFileRepository = new SourceFileRepository(_context.Project, _dbPath);
        ImportRepository = new ImportRepository(_context.Project, _dbPath);
        ClassRepository = new ClassRepository(_context.Project, _dbPath);
        MethodRepository = new MethodRepository(_context.Project, _dbPath);
        InvocationRepository = new InvocationRepository(_context.Project, _dbPath);
        PropertyRepository = new PropertyRepository(_context.Project, _dbPath);
        // Coverage
        CoverageReportRepository = new CoverageReportRepository(_context.Project, _dbPath);
        PackageCoverageRepository = new PackageCoverageRepository(_context.Project, _dbPath);
        ClassCoverageRepository = new ClassCoverageRepository(_context.Project, _dbPath);
        MethodCoverageRepository = new MethodCoverageRepository(_context.Project, _dbPath);
        
        // test results
        TestResultRepository = new TestResultRepository(_context.Project, _dbPath);
        TestRunRepository = new TestRunRepository(_context.Project, _dbPath);
        
        // generated tests
        GeneratedTestRepository = new GeneratedTestRepository(_context.Project, _dbPath);
        
        // Mutation
        FileMutationResultRepository = new FileMutationResultRepository(_context.Project, _dbPath);
        MutationReportRepository = new MutationReportRepository(_context.Project, _dbPath);
        MutantTestMapRepository = new MutantTestMapRepository(_context.Project, _dbPath);
        MutantRepository = new MutantRepository(_context.Project, _dbPath);
        
        // Metrics
        LizardFileCodeMetricsRepository = new LizardFileCodeMetricsRepository(_context.Project, _dbPath);
        LizardFunctionCodeMetricsRepository = new LizardFunctionCodeMetricsRepository(_context.Project, _dbPath);
        
        // Test Smells
        MethodTestSmellRepository = new MethodTestSmellRepository(_context.Project, _dbPath);
        TestSmellRepository = new TestSmellRepository(_context.Project, _dbPath);

    }

    /// <summary>
    /// Initialize DB - creates file if needed and runs migration scripts.
    /// </summary>
    public async Task InitializeAsync()
    {
        _context.Project.Logger?.Information($"Initializing SQLite DB at {_dbPath}");
        try
        {
            await using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync();

            await using var cmd = new SqliteCommand(Migrations.Schema, conn);
            await cmd.ExecuteNonQueryAsync();

            _context.Project.Logger?.Information("SQLite migrations executed successfully.");
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Error($"Error executing migrations: {ex.Message}");
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
        foreach (var solution in _context.Project.Solutions) solution.ProjectModelId = _context.Project.DbId;
    }


    public void ApplySolutionIdToProjects()
    {
        foreach (var project in _context.Project.Projects)
            project.SolutionId =
                _context.Project.Solutions.Find(x => x.SolutionFilePath == project.SolutionFilePath)?.Id ?? 0;
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
                
                // Handle nullable numeric columns
                LineRate = reader.IsDBNull(3) ? (double)(0.0) : reader.GetDouble(3),
                BranchRate = reader.IsDBNull(4) ? (double)(0.0) : reader.GetDouble(4),

                ClassId = reader.GetInt32(5),
                ClassName = reader.GetString(6),

                CoverageStatus = reader.GetString(7),

                TestMethodId = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                TestMethodName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                TestMethodBody = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                TestClassId = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                TestClassName = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                TestClassBody = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                TestFramework = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),

                TestClassLineStart = reader.IsDBNull(15) ? 0 : reader.GetInt32(15),
                TestClassBodyStart = reader.IsDBNull(16) ? 0 : reader.GetInt32(16),
                TestClassLineEnd = reader.IsDBNull(17) ? 0 : reader.GetInt32(17),
                TestClassBodyEnd = reader.IsDBNull(18) ? 0 : reader.GetInt32(18),

                TestFilePath = reader.IsDBNull(19) ? string.Empty : reader.GetString(19),
                TestDependencies = reader.IsDBNull(20) ? string.Empty : reader.GetString(20),
                TestNamespace = reader.IsDBNull(21) ? string.Empty : reader.GetString(21),

                SolutionFilePath = reader.IsDBNull(22) ? string.Empty : reader.GetString(22)
            };

            results.Add(item);
        }

        return results;
    }
    
    public async Task<bool> HasCandidateMethods()
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT COUNT(*) FROM v_baseline_uncovered_tested_methods;
        ";
        

        var count = (long)await checkCmd.ExecuteScalarAsync();
        return count > 0;
    }
}