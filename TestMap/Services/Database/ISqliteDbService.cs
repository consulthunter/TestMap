using Microsoft.Data.Sqlite;

namespace TestMap.Services.Database;

public interface ISqliteDatabaseService
{
    Task InitializeAsync();
    Task<SqliteConnection> GetOpenConnectionAsync();

    Task InsertProjectInformation();
}