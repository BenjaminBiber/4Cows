using Microsoft.VisualBasic.FileIO;
using MySqlConnector;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BBCowDataLibrary.SQL
{
    public static class DatabaseService
    {
        private static string connectionString = "";

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

        public static async Task<bool> ExecuteQueryAsync(Func<MySqlCommand, Task> commandAction)
        {
            var success = false;
            try
            {
                using var connection = await OpenConnectionAsync();
                using var command = connection.CreateCommand();
                await commandAction(command);
                success = true;
            }catch(Exception ex)
            {
                Console.WriteLine(ex);
                success = false;
            }
            return success;
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

        public static void GetDBStringFromCSV()
        {
            var userPath = @$"C:\Users\{Environment.UserName}\4Cows";
            if(!Directory.Exists(userPath))
            {
                Directory.CreateDirectory(userPath);
            }
            if(File.Exists($@"{userPath}\DbString.csv"))
            {
                userPath = userPath + @"\DbString.csv";
                try
                {
                    using (TextFieldParser parser = new TextFieldParser(userPath))
                    {
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters(";");

                        while (!parser.EndOfData)
                        {
                            string[] fields = parser.ReadFields();
                            if (fields.Count() > 0 && fields != null)
                            {
                                if (Regex.IsMatch(fields[0], @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
                                {
                                    connectionString = new MySqlConnectionStringBuilder
                                    {
                                        Server = fields[0],
                                        UserID = fields[1] ?? "",
                                        Password = fields[2] ?? "",
                                        Database = fields[3] ?? "",
                                        Port = 3306
                                    }.ConnectionString;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler beim Lesen der Datei: {ex.Message}");
                }
            }
        }
    }
}
