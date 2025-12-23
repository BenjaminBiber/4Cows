using MySqlConnector;
using System;
using System.Threading.Tasks;
using BB_Cow.Services;

namespace BBCowDataLibrary.SQL
{
    public static class DatabaseService
    {
        private static string connectionString = "";
        private static bool _isConnected;

        public static bool HasActiveConnection => _isConnected;

        public static event Action<bool>? ConnectionStatusChanged;

        public static async Task<MySqlConnection> OpenConnectionAsync()
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            UpdateConnectionStatus(true);
            return connection;
        }

        public static async Task<bool> IsConfigured()
        {
            try
            {
                using var connection = await OpenConnectionAsync();
                connection.Close();
            }
            catch (MySqlException ex)
            {
                LoggerService.LogError(typeof(DatabaseService), "Database connection failed, with {@Message}", ex, ex.Message);
                UpdateConnectionStatus(false);
                return false;
            }
            LoggerService.LogInformation(typeof(DatabaseService), "Database connection successful, with {@connectionString}", connectionString);
            UpdateConnectionStatus(true);
            return true;
        }

        public static async Task<bool> ExecuteQueryAsync(Func<MySqlCommand, Task> commandAction)
        {
            var success = false;
            try
            {
                using var connection = await OpenConnectionAsync();
                using var command = connection.CreateCommand();
                await commandAction(command);
                success = true;
            }
            catch (MySqlException ex)
            {
                LoggerService.LogError(typeof(DatabaseService), "Database query failed, with {@Message}", ex, ex.Message);
                UpdateConnectionStatus(false);
                success = false;
            }
            catch(Exception ex)
            {
                LoggerService.LogError(typeof(DatabaseService), "Database query failed, with {@Message}", ex, ex.Message);
                success = false;
            }
            return success;
        }

        public static async Task<List<T>> ReadDataAsync<T>(string query, Func<MySqlDataReader, T> readAction, object parameters = null)
        {
            var items = new List<T>();
            if (await IsConfigured())
            {
                try
                {
                    await ExecuteQueryAsync(async command =>
                    {
                        command.CommandText = query;

                        if (parameters != null)
                        {
                            foreach (var prop in parameters.GetType().GetProperties())
                            {
                                var value = prop.GetValue(parameters);
                                command.Parameters.AddWithValue($"@{prop.Name}", value ?? DBNull.Value);
                            }
                        }

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
                    LoggerService.LogError(typeof(DatabaseService), "Database query failed, with {@Message}", ex, ex.Message);
                    UpdateConnectionStatus(false);
                    return new List<T>();
                }
            }
            return new List<T>();
        }

        public static void GetDBStringFromEnvironment()
        {
            connectionString = new MySqlConnectionStringBuilder
            {
                Server = Environment.GetEnvironmentVariable("DB_SERVER") ?? "127.0.0.1",
                UserID = Environment.GetEnvironmentVariable("DB_User") ?? "root",
                Password = Environment.GetEnvironmentVariable("DB_Password") ?? "admin",
                Database = Environment.GetEnvironmentVariable("DB_DB") ?? "4cows_v2",
                Port = 3306
            }.ConnectionString;

            IsConfigured().Wait();
        }

        public static string GetConnectionString()
        {
            return connectionString;
        }

        private static void UpdateConnectionStatus(bool isConnected)
        {
            if (_isConnected == isConnected)
            {
                return;
            }

            _isConnected = isConnected;
            ConnectionStatusChanged?.Invoke(_isConnected);
        }
    }
}
