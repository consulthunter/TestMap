using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Database;

namespace TestMap.Services.Database;

public class SqliteDatabaseService : ISqliteDatabaseService
{
    private readonly ProjectModel _projectModel;
    private readonly string _dbPath;
    private readonly string _migrationSql;

    public SqliteDatabaseService(ProjectModel projectModel)
    {
        _projectModel = projectModel;

        // Set DB path under the project output directory, e.g. output/<runDate>/<projectId>/project.db
        var dbFolder = Path.Combine(_projectModel.OutputPath ?? string.Empty);
        if (!Directory.Exists(dbFolder)) Directory.CreateDirectory(dbFolder);

        _dbPath = Path.Combine(dbFolder, "analysis.db");

        // Load migration SQL file - put your migrations.sql path here or embed as resource
        var migrationsFilePath = _projectModel.Config.FilePaths.MigrationsFilePath;
        if (File.Exists(migrationsFilePath))
            _migrationSql = File.ReadAllText(migrationsFilePath);
        else
        {
            _projectModel.Logger?.Warning($"Migration SQL file not found at {migrationsFilePath}");
            _migrationSql = string.Empty;
        }
    }

    /// <summary>
    /// Initialize DB - creates file if needed and runs migration scripts.
    /// </summary>
    public async Task InitializeAsync()
    {
        _projectModel.Logger?.Information($"Initializing SQLite DB at {_dbPath}");

        // Create empty DB file if doesn't exist
        if (!File.Exists(_dbPath))
        {
            SQLiteConnection.CreateFile(_dbPath);
            _projectModel.Logger?.Information("SQLite DB file created.");
        }

        // Run migrations (if any)
        if (!string.IsNullOrWhiteSpace(_migrationSql))
        {
            try
            {
                using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
                await conn.OpenAsync();

                using var cmd = new SQLiteCommand(_migrationSql, conn);
                await cmd.ExecuteNonQueryAsync();

                _projectModel.Logger?.Information("SQLite migrations executed successfully.");
            }
            catch (Exception ex)
            {
                _projectModel.Logger?.Error($"Error executing migrations: {ex.Message}");
                throw;
            }
        }
        else
        {
            _projectModel.Logger?.Information("No migration SQL to execute.");
        }
    }

    /// <summary>
    /// Opens and returns a SQLiteConnection
    /// Caller is responsible for disposing the connection
    /// </summary>
    public async Task<SQLiteConnection> GetOpenConnectionAsync()
    {
        var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();
        return conn;
    }

    public async Task InsertProjectInformation()
    {
        await InsertProjectModelGetId();
        ApplyProjectModelIdToSolutions();
        await InsertAnalysisSolutionGetId();
        ApplySolutionIdToProjects();
        await InsertAnalysisProjectGetId();
    }

    public async Task InsertProjectModelGetId()
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();

        // First, check if the project model
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM projects
            WHERE owner = @owner
              AND repo_name = @repoName;
        ";

        checkCmd.Parameters.AddWithValue("@owner", _projectModel.Owner);
        checkCmd.Parameters.AddWithValue("@repoName", _projectModel.RepoName);

        var existingId = await checkCmd.ExecuteScalarAsync();
        if (existingId != null && existingId != DBNull.Value)
        {
            _projectModel.DbId = int.Parse(existingId.ToString()!);
        }
        else
        {
            var createdAt = DateTime.UtcNow;
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO projects (
                   owner, repo_name, directory_path, web_url, database_path, last_analyzed_commit, created_at
                ) VALUES (
                    @owner, @repoName, @directoryPath, @webUrl, @databasePath, @lastAnalyzedCommit, @createdAt
                );
            ";

            insertCmd.Parameters.AddWithValue("@owner", _projectModel.Owner);
            insertCmd.Parameters.AddWithValue("@repoName", _projectModel.RepoName);
            insertCmd.Parameters.AddWithValue("@directoryPath", _projectModel.DirectoryPath);
            insertCmd.Parameters.AddWithValue("@webUrl", _projectModel.GitHubUrl);
            insertCmd.Parameters.AddWithValue("@databasePath", _dbPath);
            insertCmd.Parameters.AddWithValue("@lastAnalyzedCommit", _projectModel.LastAnalyzedCommit);
            insertCmd.Parameters.AddWithValue("@createdAt", createdAt);

            await insertCmd.ExecuteNonQueryAsync();
        
            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64) await lastIdCmd.ExecuteScalarAsync();
            _projectModel.DbId = (int)newId;
        }
    }

    public void ApplyProjectModelIdToSolutions()
    {
        foreach (var solution in _projectModel.Solutions)
        {
            solution.ProjectModelId = _projectModel.DbId;
        }
    }

    public async Task InsertAnalysisSolutionGetId()
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();

        // First, check if the solution exists

        foreach (var solution in _projectModel.Solutions)
        {
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = @"
                SELECT id,project_id,guid  FROM analysis_solutions
                WHERE solution_path = @solution_path;
            ";

            checkCmd.Parameters.AddWithValue("@solution_path", solution.SolutionFilePath);

            using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int id = reader.GetInt16(0);
                int projectId = reader.GetInt16(1);
                string guid = reader.GetString(2);
                
                solution.Id = id;
                solution.ProjectModelId = projectId;
                solution.Guid = guid;
            }
            else
            {
                var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO analysis_solutions (
                       project_id, solution_path, guid
                    ) VALUES (
                        @project_id, @solution_path, @guid
                    );
                ";
                
                insertCmd.Parameters.AddWithValue("@project_id", _projectModel.DbId);
                insertCmd.Parameters.AddWithValue("@solution_path", solution.SolutionFilePath);
                insertCmd.Parameters.AddWithValue("@guid", solution.Guid);

                await insertCmd.ExecuteNonQueryAsync();
        
                var lastIdCmd = conn.CreateCommand();
                lastIdCmd.CommandText = "SELECT last_insert_rowid();";
                var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
                solution.Id = (int)newId;
            }
        }
    }

    public void ApplySolutionIdToProjects()
    {
        foreach (var project in _projectModel.Projects)
        {
            project.SolutionId = _projectModel.Solutions.Find(x => x.SolutionFilePath == project.SolutionFilePath)?.Id ?? 0;
        }
    }

    public async Task InsertAnalysisProjectGetId()
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();

        // First, check if the project exists

        foreach (var project in _projectModel.Projects)
        {
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = @"
                SELECT id, solution_id, guid   FROM analysis_projects
                WHERE project_path = @project_path
                 AND target_framework = @target_framework;
            ";

            checkCmd.Parameters.AddWithValue("@project_path", project.ProjectFilePath);
            checkCmd.Parameters.AddWithValue("@target_framework", project.LanguageFramework);
            
            using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int id = reader.GetInt16(0);
                int solutionId = reader.GetInt16(1);
                string guid = reader.GetString(2);
                
                project.Id = id;
                project.SolutionId = solutionId;
                project.Guid = guid;
            }
            else
            {
                var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO analysis_projects (
                       solution_id, guid, project_path, target_framework
                    ) VALUES (
                        @solution_id, @guid, @project_path, @target_framework
                    );
                ";
                
                insertCmd.Parameters.AddWithValue("@solution_id", project.SolutionId);
                insertCmd.Parameters.AddWithValue("@guid", project.Guid);
                insertCmd.Parameters.AddWithValue("@project_path", project.ProjectFilePath);
                insertCmd.Parameters.AddWithValue("@target_framework", project.LanguageFramework);

                await insertCmd.ExecuteNonQueryAsync();
        
                var lastIdCmd = conn.CreateCommand();
                lastIdCmd.CommandText = "SELECT last_insert_rowid();";
                var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
                project.Id = (int)newId;
            }
        }
    }

    public async Task InsertPackageGetId(PackageModel package)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();

        // First, check if the package already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM source_packages
            WHERE analysis_project_id = @analysisProjectId
              AND package_name = @packageName
              AND package_path = @packagePath;
        ";

        checkCmd.Parameters.AddWithValue("@analysisProjectId", package.ProjectId);
        checkCmd.Parameters.AddWithValue("@packageName", package.Name);
        checkCmd.Parameters.AddWithValue("@packagePath", package.Path);

            
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            string guid = reader.GetString(1);
                
            package.Id = id;
            package.Guid = guid;
        }
        
        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO source_packages (
                analysis_project_id, guid, package_name, package_path 
            ) VALUES (
                @analysisProjectId, @guid, @packageName, @packagePath 
            );
        ";
        
        insertCmd.Parameters.AddWithValue("@analysisProjectId", package.ProjectId);
        insertCmd.Parameters.AddWithValue("@guid", package.Guid);
        insertCmd.Parameters.AddWithValue("@packageName", package.Name);
        insertCmd.Parameters.AddWithValue("@packagePath", package.Path);
        

        await insertCmd.ExecuteNonQueryAsync();
        
        var lastIdCmd = conn.CreateCommand();
        lastIdCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
        package.Id = (int)newId;
    }
    
    
    public async Task InsertFileGetId(FileModel file)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();

        // First, check if the file already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM source_files
            WHERE package_id = @package_id
              AND namespace = @namespace
              AND path = @path;
        ";
        
        checkCmd.Parameters.AddWithValue("@package_id", file.PackageId);
        checkCmd.Parameters.AddWithValue("@namespace", file.Namespace);
        checkCmd.Parameters.AddWithValue("@path", file.FilePath);
            
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            string guid = reader.GetString(1);
                
            file.Id = id;
            file.Guid = guid;
            
        }
        
        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO source_files (
                package_id, guid, namespace, name, language, meta_data, usings, path
            ) VALUES (
                @package_id, @guid, @namespace, @name, @language, @meta_data, @usings, @path
            );
        ";
        
        insertCmd.Parameters.AddWithValue("@package_id", file.PackageId);
        insertCmd.Parameters.AddWithValue("@guid", file.Guid);
        insertCmd.Parameters.AddWithValue("@namespace", file.Namespace);
        insertCmd.Parameters.AddWithValue("@name", file.Name);
        insertCmd.Parameters.AddWithValue("@language", file.Language);
        insertCmd.Parameters.AddWithValue("@meta_data", file.MetaData);
        insertCmd.Parameters.AddWithValue("@usings", file.UsingStatements);
        insertCmd.Parameters.AddWithValue("@path", file.FilePath);
        

        await insertCmd.ExecuteNonQueryAsync();
        
        var lastIdCmd = conn.CreateCommand();
        lastIdCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
        file.Id = (int)newId;
    }
    public async Task InsertImports(ImportModel import)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();

        // First, check if the import already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM imports
            WHERE import_name = @importName
              AND import_path = @importPath
              AND is_local = @isLocal;
        ";

        checkCmd.Parameters.AddWithValue("@importName", import.ImportName);
        checkCmd.Parameters.AddWithValue("@importPath", import.ImportPath);
        checkCmd.Parameters.AddWithValue("@isLocal", import.IsLocal);
        
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            string guid = reader.GetString(1);
                
            import.Id = id;
            import.Guid = guid;
            
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO imports (
                file_id, guid, import_name, import_path, full_string, is_local
            ) VALUES (
                @fileId, @guid, @importName, @importPath, @fullString, @isLocal
            );
        ";
            
            insertCmd.Parameters.AddWithValue("@fileId", import.FileId);
            insertCmd.Parameters.AddWithValue("@guid", import.Guid);
            insertCmd.Parameters.AddWithValue("@importName", import.ImportName);
            insertCmd.Parameters.AddWithValue("@importPath", import.ImportPath);
            insertCmd.Parameters.AddWithValue("@fullString", import.FullString);
            insertCmd.Parameters.AddWithValue("@isLocal", import.IsLocal);

            await insertCmd.ExecuteNonQueryAsync();
        
            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
            import.Id = (int)newId;
        }
    }

    public async Task InsertClassesGetId(ClassModel cla)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();

        // First, check if the class already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM classes
            WHERE file_id = @fileId
              AND name = @name;
        ";

        checkCmd.Parameters.AddWithValue("@fileId", cla.FileId);
        checkCmd.Parameters.AddWithValue("@name", cla.Name);
        
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            string guid = reader.GetString(1);
                
            cla.Id = id;
            cla.Guid = guid;
            
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO classes (
                file_id, guid, name, visibility, modifiers, attributes, full_string, doc_string, is_test_class,
                                 testing_framework, location_start_lin_no, location_body_start, location_body_end,
                                 location_end_lin_no
            ) VALUES (
                @fileId, @guid, @name, @visibility, @modifiers, @attributes, @fullString, @docString, @isTestClass,
                      @testingFramework, @locationStartLinNo, @locationBodyStart, @locationBodyEnd, @locationEndLinNo
            );
        ";
            
            insertCmd.Parameters.AddWithValue("@fileId", cla.FileId);
            insertCmd.Parameters.AddWithValue("@guid", cla.Guid);
            insertCmd.Parameters.AddWithValue("@name", cla.Name);
            insertCmd.Parameters.AddWithValue("@visibility", cla.Visibility);
            insertCmd.Parameters.AddWithValue("@modifiers", cla.Modifiers); 
            insertCmd.Parameters.AddWithValue("@attributes", cla.Attributes);
            insertCmd.Parameters.AddWithValue("@fullString", cla.FullString);
            insertCmd.Parameters.AddWithValue("@docString", cla.DocString);
            insertCmd.Parameters.AddWithValue("@isTestClass", cla.IsTestClass);
            insertCmd.Parameters.AddWithValue("@testingFramework", cla.TestingFramework);
            insertCmd.Parameters.AddWithValue("@locationStartLinNo", cla.Location.StartLineNumber);
            insertCmd.Parameters.AddWithValue("@locationBodyStart", cla.Location.BodyStartPosition);
            insertCmd.Parameters.AddWithValue("@locationBodyEnd", cla.Location.BodyEndPosition);
            insertCmd.Parameters.AddWithValue("@locationEndLinNo", cla.Location.EndLineNumber);

            await insertCmd.ExecuteNonQueryAsync();
        
            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
            cla.Id = (int)newId;
        }
    }
    
    public async Task InsertMethodsGetId(MethodModel method)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();
        
        // First, check if the method already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM methods
            WHERE class_id = @classId
              AND name = @name;
        ";

        checkCmd.Parameters.AddWithValue("@classId", method.ClassId);
        checkCmd.Parameters.AddWithValue("@name", method.Name);
        
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            string guid = reader.GetString(1);
                
            method.Id = id;
            method.Guid = guid;
            
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO methods (
              class_id, guid, name, visibility, modifiers, attributes, full_string, doc_string, is_test_method,
                                 testing_framework, test_type, location_start_lin_no, location_body_start, location_body_end,
                                 location_end_lin_no
            ) VALUES (
                      @classId, @guid, @name, @visibility, @modifiers, @attributes, @fullString, @docString, @isTestMethod,
                      @testingFramework, @testType, @locationStartLinNo, @locationBodyStart, @locationBodyEnd, @locationEndLinNo
            );
        ";
            
            insertCmd.Parameters.AddWithValue("@classId", method.ClassId);
            insertCmd.Parameters.AddWithValue("@guid", method.Guid);
            insertCmd.Parameters.AddWithValue("@name", method.Name);
            insertCmd.Parameters.AddWithValue("@visibility", method.Visibility);
            insertCmd.Parameters.AddWithValue("@modifiers", method.Modifiers); 
            insertCmd.Parameters.AddWithValue("@attributes", method.Attributes);
            insertCmd.Parameters.AddWithValue("@fullString", method.FullString);
            insertCmd.Parameters.AddWithValue("@docString", method.DocString);
            insertCmd.Parameters.AddWithValue("@isTestMethod", method.IsTestMethod);
            insertCmd.Parameters.AddWithValue("@testingFramework", method.TestingFramework);
            insertCmd.Parameters.AddWithValue("@testType", method.TestType);
            insertCmd.Parameters.AddWithValue("@locationStartLinNo", method.Location.StartLineNumber);
            insertCmd.Parameters.AddWithValue("@locationBodyStart", method.Location.BodyStartPosition);
            insertCmd.Parameters.AddWithValue("@locationBodyEnd", method.Location.BodyEndPosition);
            insertCmd.Parameters.AddWithValue("@locationEndLinNo", method.Location.EndLineNumber);
            

            await insertCmd.ExecuteNonQueryAsync();
        
            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
            method.Id = (int)newId;
        }
    }

    public async Task InsertInvocationsGetId(InvocationModel invocation)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();
        
        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM invocations
            WHERE target_method_id = @targetMethodId
              AND full_string = @fullString;
        ";

        checkCmd.Parameters.AddWithValue("@targetMethodId", invocation.TargetMethodId);;
        checkCmd.Parameters.AddWithValue("@fullString", invocation.FullString);
        
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            string guid = reader.GetString(1);
                
            invocation.Id = id;
            invocation.Guid = guid;
            
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO invocations (
              target_method_id, source_method_id, guid, is_assertion, full_string, location_start_lin_no, location_body_start, location_body_end, location_end_lin_no
            ) VALUES (
                      @targetMethodId, @sourceMethodId, @guid, @isAssertion, @fullString, @locationStartLinNo, @locationBodyStart, @locationBodyEnd, @locationEndLinNo
            );
        ";
            
            insertCmd.Parameters.AddWithValue("@targetMethodId", invocation.TargetMethodId);
            insertCmd.Parameters.AddWithValue("@sourceMethodId", invocation.SourceMethodId);
            insertCmd.Parameters.AddWithValue("@guid", invocation.Guid);
            insertCmd.Parameters.AddWithValue("@isAssertion", invocation.IsAssertion);
            insertCmd.Parameters.AddWithValue("@fullString", invocation.FullString);
            insertCmd.Parameters.AddWithValue("@locationStartLinNo", invocation.Location.StartLineNumber);
            insertCmd.Parameters.AddWithValue("@locationBodyStart", invocation.Location.BodyStartPosition);
            insertCmd.Parameters.AddWithValue("@locationBodyEnd", invocation.Location.BodyEndPosition);
            insertCmd.Parameters.AddWithValue("@locationEndLinNo", invocation.Location.EndLineNumber);
            

            await insertCmd.ExecuteNonQueryAsync();
        
            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
            invocation.Id = (int)newId;
        }
    }

    public async Task InsertPropertyGetId(PropertyModel property)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();
        
        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM properties
            WHERE class_id = @classId
              AND name = @name;
        ";

        checkCmd.Parameters.AddWithValue("@classId", property.ClassId);
        checkCmd.Parameters.AddWithValue("@name", property.Name);
        
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            string guid = reader.GetString(1);
                
            property.Id = id;
            property.Guid = guid;
            
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO properties (
              class_id, guid, name, visibility, modifiers, attributes, full_string, location_start_lin_no, location_body_start, location_body_end, location_end_lin_no
            ) VALUES (
                      @classId, @guid, @name, @visibility, @modifiers, @attributes, @fullString, @locationStartLinNo, @locationBodyStart, @locationBodyEnd, @locationEndLinNo
            );
        ";
            
            insertCmd.Parameters.AddWithValue("@classId", property.ClassId);
            insertCmd.Parameters.AddWithValue("@guid", property.Guid);
            insertCmd.Parameters.AddWithValue("@name", property.Name);
            insertCmd.Parameters.AddWithValue("@visibility", property.Visibility);
            insertCmd.Parameters.AddWithValue("@modifiers", property.Modifiers); 
            insertCmd.Parameters.AddWithValue("@attributes", property.Attributes);
            insertCmd.Parameters.AddWithValue("@fullString", property.FullString);
            insertCmd.Parameters.AddWithValue("@locationStartLinNo", property.Location.StartLineNumber);
            insertCmd.Parameters.AddWithValue("@locationBodyStart", property.Location.BodyStartPosition);
            insertCmd.Parameters.AddWithValue("@locationBodyEnd", property.Location.BodyEndPosition);
            insertCmd.Parameters.AddWithValue("@locationEndLinNo", property.Location.EndLineNumber);

            await insertCmd.ExecuteNonQueryAsync();
        
            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
            property.Id = (int)newId;
        }
    }

    public async Task<List<InvocationDetails>> GetUnresolvedInvocations()
    {
        var results = new List<InvocationDetails>();

        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                i.id, i.target_method_id, i.source_method_id, i.guid, i.full_string,
                m.id, m.class_id, m.guid, m.name,
                c.id, c.file_id, c.guid, c.name,
                s.name, s.path, s.package_id, s.guid,
                sp.id, sp.package_name, sp.analysis_project_id, sp.guid,
                ap.id, ap.project_path, ap.solution_id, ap.guid,
                asol.id, asol.solution_path, asol.guid
            FROM invocations AS i
            JOIN methods AS m ON i.target_method_id = m.id
            JOIN classes AS c ON m.class_id = c.id
            JOIN source_files AS s ON c.file_id = s.id
            JOIN source_packages AS sp ON s.package_id = sp.id
            JOIN analysis_projects AS ap ON sp.analysis_project_id = ap.id
            JOIN analysis_solutions AS asol ON ap.solution_id = asol.id
            WHERE i.source_method_id = 0;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var item = new InvocationDetails
            {
                InvocationId      = reader.GetInt32(0),
                TargetMethodId    = reader.GetInt32(1),
                SourceMethodId    = reader.GetInt32(2),
                InvocationGuid    = reader.GetString(3),
                FullString        = reader.GetString(4),

                MethodId          = reader.GetInt32(5),
                MethodClassId     = reader.GetInt32(6),
                MethodGuid        = reader.GetString(7),
                MethodName        = reader.GetString(8),

                ClassId           = reader.GetInt32(9),
                FileId            = reader.GetInt32(10),
                ClassGuid         = reader.GetString(11),
                ClassName         = reader.GetString(12),

                FileName          = reader.GetString(13),
                FilePath          = reader.GetString(14),
                PackageId         = reader.GetInt32(15),
                FileGuid          = reader.GetString(16),

                SourcePackageId   = reader.GetInt32(17),
                PackageName       = reader.GetString(18),
                AnalysisProjectId = reader.GetInt32(19),
                PackageGuid       = reader.GetString(20),

                ProjectId         = reader.GetInt32(21),
                ProjectPath       = reader.GetString(22),
                SolutionId        = reader.GetInt32(23),
                ProjectGuid       = reader.GetString(24),

                SolutionDbId      = reader.GetInt32(25),
                SolutionPath      = reader.GetString(26),
                SolutionGuid      = reader.GetString(27),
            };

            results.Add(item);
        }

        return results;
    }

    public async Task<int> FindMethod(string name, string filepath)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT m.id
        FROM methods AS m
        JOIN classes AS c ON m.class_id = c.id
        JOIN source_files AS sf ON c.file_id = sf.id
        WHERE m.name = @name AND sf.path = @filepath;
    ";

        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@filepath", filepath);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt32(0); // m.guid
        }

        return 0; // not found
    }
    
    public async Task UpdateInvocationSourceId(int invocationId, int sourceMethodId)
    {
        using var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        UPDATE invocations
        SET source_method_id = @sourceMethodId
        WHERE id = @invocationId;
    ";

        cmd.Parameters.AddWithValue("@sourceMethodId", sourceMethodId);
        cmd.Parameters.AddWithValue("@invocationId", invocationId);

        await cmd.ExecuteNonQueryAsync();
    }
}