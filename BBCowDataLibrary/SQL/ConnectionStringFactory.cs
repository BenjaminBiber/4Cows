using MySqlConnector;

namespace BBCowDataLibrary.SQL;

public static class ConnectionStringFactory
{
    public static string Create(DatabaseConnectionSettings settings)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = settings.Server,
            UserID = settings.User,
            Password = settings.Password,
            Database = settings.Database,
            Port = settings.Port
        };

        return builder.ConnectionString;
    }
}
