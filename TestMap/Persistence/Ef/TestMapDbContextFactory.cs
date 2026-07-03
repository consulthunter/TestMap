using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TestMap.Persistence.Ef;

public class TestMapDbContextFactory : IDesignTimeDbContextFactory<TestMapDbContext>
{
    public TestMapDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestMapDbContext>();

        optionsBuilder.UseSqlite("Data Source=testmap.db");

        return new TestMapDbContext(optionsBuilder.Options);
    }
}