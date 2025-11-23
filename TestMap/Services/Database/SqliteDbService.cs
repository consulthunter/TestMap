using System.Data;
using Microsoft.Data.Sqlite;

using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Coverage;
using TestMap.Models.Database;
using TestMap.Models.Results;

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

        // Run migrations (if any)
        if (!string.IsNullOrWhiteSpace(_migrationSql))
        {
            try
            {
                await using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                await using var cmd = new SqliteCommand(_migrationSql, conn);
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
    public async Task<SqliteConnection> GetOpenConnectionAsync()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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
            insertCmd.Parameters.AddWithValue("@lastAnalyzedCommit", _projectModel.LastAnalyzedCommit ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@createdAt", createdAt);

            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)await lastIdCmd.ExecuteScalarAsync();
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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
            
            await using var reader = await checkCmd.ExecuteReaderAsync();
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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
            
        await using var reader = await checkCmd.ExecuteReaderAsync();
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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
        
        await using var reader = await checkCmd.ExecuteReaderAsync();
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        
        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM invocations
            WHERE target_method_id = @targetMethodId
              AND full_string = @fullString;
        ";

        checkCmd.Parameters.AddWithValue("@targetMethodId", invocation.TargetMethodId);
        checkCmd.Parameters.AddWithValue("@fullString", invocation.FullString);
        
        await using var reader = await checkCmd.ExecuteReaderAsync();
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


            try
            {
                await insertCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
            invocation.Id = (int)newId;
        }
    }

    public async Task InsertPropertyGetId(PropertyModel property)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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

        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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

    public async Task GetImports()
    {
        var results = new List<ImportModel>();

        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
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

           // results.Add(item);
        }

        // return results;
        
    }
    
    public async Task<int> FindMethodFromContains(string name)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT m.id
        FROM methods AS m
        WHERE m.is_test_method = 1
         AND @name LIKE '%' || m.name || '%' COLLATE NOCASE
        LIMIT 1;
    ";

        cmd.Parameters.AddWithValue("@name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt32(0); // m.guid
        }

        return 0; // not found
    }

    public async Task InsertTestResults(List<TrxTestResult> testResults)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        await using var tx = conn.BeginTransaction();

        foreach (var testResult in testResults)
        {
            await using var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = tx;

            insertCmd.CommandText = @"
            INSERT INTO test_results (
              method_id, run_id, run_date, test_name, test_outcome, test_duration, error_message
            ) VALUES (
              @methodId, @runId, @runDate, @testName, @testOutcome, @testDuration, @testErrorMessage
            );";

            insertCmd.Parameters.AddWithValue("@methodId", testResult.MethodId);
            insertCmd.Parameters.AddWithValue("@runId", testResult.RunId);
            insertCmd.Parameters.AddWithValue("@runDate", testResult.RunDate);
            insertCmd.Parameters.AddWithValue("@testName", testResult.TestName ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@testOutcome", testResult.Outcome ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@testDuration", testResult.Duration);
            insertCmd.Parameters.AddWithValue("@testErrorMessage", (object?)testResult.ErrorMessage ?? DBNull.Value);

            await insertCmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }
    
    public async Task<int> InsertCoverageReportGetId(CoverageReport report, string runId)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        
        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM coverage_reports
            WHERE test_run_id = @runId;
        ";

        checkCmd.Parameters.AddWithValue("@runId", runId);
        
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);

            return id;

        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO coverage_reports (
              test_run_id, timestamp, line_rate, branch_rate, lines_covered, lines_valid, branches_valid, branches_covered, complexity
            ) VALUES (
                      @runId, @timestamp, @lineRate, @branchRate, @linesCovered, @linesValid, @branchesValid, @branchesCovered, @complexity
            );
        ";
            
            insertCmd.Parameters.AddWithValue("@runId", runId);
            insertCmd.Parameters.AddWithValue("@timestamp", report.Timestamp);
            insertCmd.Parameters.AddWithValue("@lineRate", report.LineRate);
            insertCmd.Parameters.AddWithValue("@branchRate", report.BranchRate);
            insertCmd.Parameters.AddWithValue("@linesCovered", report.LinesCovered);
            insertCmd.Parameters.AddWithValue("@linesValid", report.LinesValid);
            insertCmd.Parameters.AddWithValue("@branchesValid", report.BranchesValid);
            insertCmd.Parameters.AddWithValue("@branchesCovered", report.BranchesCovered);
            insertCmd.Parameters.AddWithValue("@complexity", report.Complexity);

            await insertCmd.ExecuteNonQueryAsync();
        
            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
            return (int)newId;
        }
    }
        
    public async Task<int> FindPackage(string name)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT p.id
        FROM source_packages AS p
        WHERE p.package_name = @name
        LIMIT 1;
    ";

        cmd.Parameters.AddWithValue("@name", name.Replace("-", "_"));

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt32(0); // m.guid
        }

        return 0; // not found
    }
        
    public async Task<int> InsertPackageCoverageGetId(PackageCoverage packageCoverage, int reportId, int packageId)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        
        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM package_coverage
            WHERE coverage_report_id = @reportId
            AND name = @name;
        ";

        checkCmd.Parameters.AddWithValue("@reportId", reportId);
        checkCmd.Parameters.AddWithValue("@name", packageCoverage.Name);
        
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);

            return id;

        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO package_coverage (
              coverage_report_id, package_id, name, line_rate, branch_rate, complexity
            ) VALUES (
                     @reportId, @packageId, @name, @lineRate, @branchRate, @complexity
            );
        ";
            
            insertCmd.Parameters.AddWithValue("@reportId", reportId);
            insertCmd.Parameters.AddWithValue("@packageId", packageId);
            insertCmd.Parameters.AddWithValue("@name", packageCoverage.Name);
            insertCmd.Parameters.AddWithValue("@lineRate", packageCoverage.LineRate);
            insertCmd.Parameters.AddWithValue("@branchRate", packageCoverage.BranchRate);
            insertCmd.Parameters.AddWithValue("@complexity", packageCoverage.Complexity);

            await insertCmd.ExecuteNonQueryAsync();
        
            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
            return (int)newId;
        }
    }
    
    public async Task<int> FindClass(string name)
    {
        // Extract simple class name
        string className = name.Split('.').Last();

        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT c.id
        FROM classes AS c
        WHERE c.name = @name
        LIMIT 1;
    ";
        cmd.Parameters.AddWithValue("@name", className);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt32(0); // m.guid
        }

        return 0; // not found
    }
    
        public async Task<int> InsertClassCoverageGetId(ClassCoverage classCoverage, int packageCovId, int classId)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        
        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM class_coverage
            WHERE package_coverage_id = @packageCovId
            AND name = @name;
        ";
        
        checkCmd.Parameters.AddWithValue("@packageCovId", packageCovId);
        checkCmd.Parameters.AddWithValue("@name", classCoverage.Name);
        
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);

            return id;

        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO class_coverage (
              package_coverage_id, class_id, name, line_rate, branch_rate, complexity
            ) VALUES (
                     @packageCovId, @classId, @name, @lineRate, @branchRate, @complexity
            );
        ";
            
            insertCmd.Parameters.AddWithValue("@packageCovId", packageCovId);
            insertCmd.Parameters.AddWithValue("@classId", classId);
            insertCmd.Parameters.AddWithValue("@name", classCoverage.Name);
            insertCmd.Parameters.AddWithValue("@lineRate", classCoverage.LineRate);
            insertCmd.Parameters.AddWithValue("@branchRate", classCoverage.BranchRate);
            insertCmd.Parameters.AddWithValue("@complexity", classCoverage.Complexity);

            await insertCmd.ExecuteNonQueryAsync();
        
            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
            return (int)newId;
        }
    }
        
    public async Task<int> FindMethodFromExact(string name)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT m.id
        FROM methods AS m
        WHERE m.name = @name
        LIMIT 1;
    ";

        cmd.Parameters.AddWithValue("@name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt32(0); // m.guid
        }

        return 0; // not found
    }
    
    public async Task<int> InsertMethodCoverageGetId(MethodCoverage methodCoverage, int classCoverageId, int methodId)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        
        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM method_coverage
            WHERE class_coverage_id = @classCoverageId
            AND name = @name;
        ";
        
        checkCmd.Parameters.AddWithValue("@classCoverageId", classCoverageId);
        checkCmd.Parameters.AddWithValue("@name", methodCoverage.Name);
        
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);

            return id;

        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO method_coverage (
              class_coverage_id, method_id, name, line_rate, branch_rate, complexity
            ) VALUES (
                     @classCoverageId, @methodId, @name, @lineRate, @branchRate, @complexity
            );
        ";
            insertCmd.Parameters.AddWithValue("@classCoverageId", classCoverageId);
            insertCmd.Parameters.AddWithValue("@methodId", methodId);
            insertCmd.Parameters.AddWithValue("@name", methodCoverage.Name);
            insertCmd.Parameters.AddWithValue("@lineRate", methodCoverage.LineRate);
            insertCmd.Parameters.AddWithValue("@branchRate", methodCoverage.BranchRate);
            insertCmd.Parameters.AddWithValue("@complexity", methodCoverage.Complexity);

            await insertCmd.ExecuteNonQueryAsync();
        
            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (Int64)(await lastIdCmd.ExecuteScalarAsync());
            return (int)newId;
        }
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

            SolutionFilePath = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
        };

        results.Add(item);
    }

    return results;
}

    
    public async Task<int> InsertTestRun(string runId, string runDate, string result, int coverage, string? logPath, string? error)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
        INSERT INTO test_runs (
            run_id,
            run_date,
            result,
            coverage,
            log_path,
            error
        ) VALUES (
            @runId,
            @runDate,
            @result,
            @coverage,
            @logPath,
            @error
        );
    ";

        insertCmd.Parameters.AddWithValue("@runId", runId);
        insertCmd.Parameters.AddWithValue("@runDate", runDate);
        insertCmd.Parameters.AddWithValue("@result", result);
        insertCmd.Parameters.AddWithValue("@coverage", coverage);
        insertCmd.Parameters.AddWithValue("@logPath", logPath ?? (object)DBNull.Value);
        insertCmd.Parameters.AddWithValue("@error", error ?? (object)DBNull.Value);

        await insertCmd.ExecuteNonQueryAsync();

        var lastIdCmd = conn.CreateCommand();
        lastIdCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)(await lastIdCmd.ExecuteScalarAsync());

        return (int)newId;
    }
    public async Task UpdateTestRunStatus(
        string runId,
        string result,
        int coverage,
        string? logPath,
        string? error)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        UPDATE test_runs
        SET result = @result,
            coverage = @coverage,
            log_path = @logPath,
            error = @error
        WHERE run_id = @runId;
    ";

        cmd.Parameters.AddWithValue("@result", result);
        cmd.Parameters.AddWithValue("@coverage", coverage);
        cmd.Parameters.AddWithValue("@logPath", logPath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@error", error ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@runId", runId);

        await cmd.ExecuteNonQueryAsync();
    }
}