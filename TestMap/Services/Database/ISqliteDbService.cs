using System.Data.SQLite;

namespace TestMap.Services.Database;

public interface ISqliteDatabaseService
{
    Task InitializeAsync();
    Task<SQLiteConnection> GetOpenConnectionAsync();

    Task InsertProjectInformation();
}