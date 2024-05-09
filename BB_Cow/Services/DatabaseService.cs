using MySqlConnector;

namespace BB_Cow.Services
{
    public static class DatabaseService
    {
        private static readonly string connectionString = new MySqlConnectionStringBuilder
        {
            Server = "192.168.50.222",
            UserID = "root",
            Password = "admin",
            Database = "4Cows_DB",
            Port = 3306
        }.ConnectionString;

        public static MySqlConnection OpenConnection()
        {
            var connection = new MySqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        public static void ExecuteQuery(Action<MySqlCommand> commandAction)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            commandAction(command);
        }

        public static List<T> ReadData<T>(string query, Func<MySqlDataReader, T> readAction)
        {
            var items = new List<T>();
            ExecuteQuery(command =>
            {
                command.CommandText = query;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    items.Add(readAction(reader));
                }
            });
            return items;
        }
    }
}
