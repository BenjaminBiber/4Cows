using System;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace BBCowDataLibrary.SQL;

public static class MigrationHelper
{
    public static async Task EnsureInitialMigrationRecordedAsync(DatabaseContext context, string migrationId, string productVersion)
    {
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        if (appliedMigrations.Contains(migrationId))
        {
            return;
        }

        var connection = context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            // Only backfill the migration record for a database that already contains the
            // application schema from before EF migrations were introduced. We key this on a
            // real EF-created table ('Cow') rather than "any table": other components create
            // tables early (e.g. the Serilog MariaDB sink creates 'LOGS' with autoCreateTable),
            // and counting those would falsely flag a fresh database as pre-existing, causing
            // MigrateAsync to skip creating the schema.
            await using var command = connection.CreateCommand();
            command.CommandText = @"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @schema AND table_name = 'Cow'";
            var schemaParameter = command.CreateParameter();
            schemaParameter.ParameterName = "@schema";
            schemaParameter.Value = connection.Database;
            command.Parameters.Add(schemaParameter);

            var schemaAlreadyExists = Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;

            if (!schemaAlreadyExists)
            {
                return;
            }

            await context.Database.ExecuteSqlRawAsync(@"CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
                `MigrationId` varchar(150) NOT NULL,
                `ProductVersion` varchar(32) NOT NULL,
                PRIMARY KEY (`MigrationId`)
            ) CHARACTER SET utf8mb4;");

            await context.Database.ExecuteSqlRawAsync(
                "INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ({0}, {1});",
                migrationId,
                productVersion);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
