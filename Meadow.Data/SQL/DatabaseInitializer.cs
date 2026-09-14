using System.Threading.Tasks;
using MySqlConnector;

namespace BBCowDataLibrary.SQL;

public static class DatabaseInitializer
{
    public static async Task EnsureDatabaseAsync(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            return;
        }

        var databaseName = builder.Database;
        var adminConnectionBuilder = new MySqlConnectionStringBuilder(builder.ConnectionString)
        {
            Database = string.Empty
        };

        await using var connection = new MySqlConnection(adminConnectionBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        await command.ExecuteNonQueryAsync();
    }
}
