﻿using Microsoft.VisualBasic.FileIO;
using MySqlConnector;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BB_Cow.Services;
using Microsoft.Extensions.Logging;

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
                connection.Close();
            }
            catch (MySqlException ex)
            {
                LoggerService.LogError(typeof(DatabaseService), "Database connection failed, with {@Message}", ex, ex.Message);
                return false;
            }
            LoggerService.LogInformation(typeof(DatabaseService), "Database connection successful, with {@connectionString}", connectionString);
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
            }catch(Exception ex)
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
                                        Server = fields[0] ?? "",
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
            else
            {
                GetDBStringFromEnvironment();
            }
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
    }
}
