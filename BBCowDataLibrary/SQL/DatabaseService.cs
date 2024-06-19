using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace BBCowDataLibrary.SQL
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

        public static async Task<MySqlConnection> OpenConnectionAsync()
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }

        public static async Task<bool> IsConfigured()
        {
            try
            {
                using var connection = await OpenConnectionAsync();
                return true;
            }
            catch (MySqlException ex)
            {
                return false;
            }
        }

        public static async Task ExecuteQueryAsync(Func<MySqlCommand, Task> commandAction)
        {
            using var connection = await OpenConnectionAsync();
            using var command = connection.CreateCommand();
            await commandAction(command);
        }

        public static async Task<List<T>> ReadDataAsync<T>(string query, Func<MySqlDataReader, T> readAction)
        {
            var items = new List<T>();
            if(await IsConfigured())
            {
                try
                {
                    await ExecuteQueryAsync(async command =>
                    {
                        command.CommandText = query;
                        using var reader = await command.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            items.Add(readAction(reader));
                        }
                    });
                    return items;

                }
                catch (MySqlException ex)
                {
                    Console.WriteLine(ex);
                    return new List<T>();
                }
            }
            return new List<T>();
        }
    }
}
