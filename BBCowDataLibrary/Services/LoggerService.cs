using Serilog;
using Serilog.Sinks.MariaDB.Extensions;
using System;

namespace BB_Cow.Services;

public class LoggerService
{
    private static ILogger Logger;

    public static void InitializeLogger(string logFilePath = "/tmp/Logs/log-.txt")
    {
        var logDirectory = Path.GetDirectoryName(logFilePath);
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        if (Logger == null)
        {
            Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7
                )
                .CreateLogger();

            Log.Logger = Logger;
        }
    }

    public static void InitializeDBLogger(string connectionString)
    {
        LogInformation(typeof(LoggerService), "Switched to DB Logger");
        Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.MariaDB(
                connectionString,
                autoCreateTable: true,
                tableName: "LOGS",
                useBulkInsert: true
                
            )

            .CreateLogger();

        Log.Logger = Logger;
    }

    public static void LogInformation(Type sourceClass, string message, params object[] args)
    {
        Logger?.ForContext("SourceContext", sourceClass.FullName).Information(message, args);
    }

    public static void LogWarning(Type sourceClass, string message, params object[] args)
    {
        Logger?.ForContext("SourceContext", sourceClass.FullName).Warning(message, args);
    }

    public static void LogError(Type sourceClass, string message, Exception exception = null, params object[] args)
    {
        if (exception != null)
        {
            Logger?.ForContext("SourceContext", sourceClass.FullName).Error(exception, message, args);
        }
        else
        {
            Logger?.ForContext("SourceContext", sourceClass.FullName).Error(message, args);
        }
    }

    public static void LogCritical(Type sourceClass, string message, Exception exception = null, params object[] args)
    {
        if (exception != null)
        {
            Logger?.ForContext("SourceContext", sourceClass.FullName).Fatal(exception, message, args);
        }
        else
        {
            Logger?.ForContext("SourceContext", sourceClass.FullName).Fatal(message, args);
        }
    }

    public static void LogDebug(Type sourceClass, string message, params object[] args)
    {
        Logger?.ForContext("SourceContext", sourceClass.FullName).Debug(message, args);
    }

    public static void LogTrace(Type sourceClass, string message, params object[] args)
    {
        Logger?.ForContext("SourceContext", sourceClass.FullName).Verbose(message, args);
    }

    public static void CloseLogger()
    {
        Log.CloseAndFlush();
    }
}
