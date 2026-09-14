namespace BBCowDataLibrary.SQL;

public sealed class DatabaseConnectionSettings
{
    public string Server { get; init; } = "127.0.0.1";
    public string User { get; init; } = "root";
    public string Password { get; init; } = "admin";
    public string Database { get; init; } = "4cows_v2";
    public uint Port { get; init; } = 3306;

    public static DatabaseConnectionSettings FromEnvironment() => new()
    {
        Server = Environment.GetEnvironmentVariable("DB_SERVER") ?? "127.0.0.1",
        User = Environment.GetEnvironmentVariable("DB_User") ?? "root",
        Password = Environment.GetEnvironmentVariable("DB_Password") ?? "admin",
        Database = Environment.GetEnvironmentVariable("DB_DB") ?? "4cows_v2",
        Port = uint.TryParse(Environment.GetEnvironmentVariable("DB_PORT"), out var port) ? port : 3306
    };
}
