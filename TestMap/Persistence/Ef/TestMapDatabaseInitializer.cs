using Microsoft.EntityFrameworkCore;

namespace TestMap.Persistence.Ef;

public sealed class TestMapDatabaseInitializer
{
    /// <summary>
    /// Applies any pending EF Core migrations to the database.
    /// On a blank database this creates the full schema via the InitialCreate migration.
    /// Calling this multiple times is safe — MigrateAsync is idempotent.
    /// </summary>
    public async Task InitializeAsync(TestMapDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
    }
}
