using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BBCowDataLibrary.SQL;

public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        var settings = DatabaseConnectionSettings.FromEnvironment();
        var connectionString = ConnectionStringFactory.Create(settings);
        DatabaseInitializer.EnsureDatabaseAsync(connectionString).GetAwaiter().GetResult();
        var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

        return new DatabaseContext(optionsBuilder.Options);
    }
}
