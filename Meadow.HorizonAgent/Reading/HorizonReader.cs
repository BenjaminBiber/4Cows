using Meadow.HorizonAgent.Options;
using Meadow.Shared.Models.Horizon;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meadow.HorizonAgent.Reading;

/// <summary>Liest die Tiere aus der Horizon-Datenbank - ausschliesslich lesend.</summary>
public sealed class HorizonReader : IHorizonReader
{
    public const string ReadOnlyUser = "BenchmarkReadOnly";

    private readonly HorizonOptions _options;
    private readonly ILogger<HorizonReader> _logger;

    public HorizonReader(IOptions<HorizonOptions> options, ILogger<HorizonReader> logger)
    {
        _options = options.Value;
        _logger = logger;
        EnsurePoolingDisabled(_options.ConnectionString);
    }

    /// <summary>
    /// Eine Verbindung mit NO REVERT laesst sich nicht in den Pool zuruecksetzen. Waere Pooling an,
    /// bekaeme irgendwann jemand anders unsere eingeschraenkte Verbindung - oder wir seine.
    /// </summary>
    public static void EnsurePoolingDisabled(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Horizon:ConnectionString ist nicht gesetzt.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (builder.Pooling)
        {
            throw new InvalidOperationException(
                "Horizon:ConnectionString muss 'Pooling=false' enthalten: eine Verbindung mit " +
                "EXECUTE AS ... WITH NO REVERT laesst sich nicht in den Verbindungspool zuruecksetzen.");
        }
    }

    public async Task<List<HorizonAnimal>> ReadAnimalsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AnimalQuery))
        {
            throw new InvalidOperationException("Horizon:AnimalQuery ist nicht gesetzt.");
        }

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Jeder lokale Windows-Benutzer ist auf diesem Server db_owner (guest). Deshalb als
        // ERSTES auf den Nur-Lese-Benutzer wechseln, ohne Rueckweg.
        await using (var impersonate = new SqlCommand($"EXECUTE AS USER = '{ReadOnlyUser}' WITH NO REVERT;", connection))
        {
            await impersonate.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var who = new SqlCommand("SELECT USER_NAME();", connection))
        {
            var user = (string?)await who.ExecuteScalarAsync(cancellationToken);
            if (!string.Equals(user, ReadOnlyUser, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Nur-Lese-Absicherung greift nicht (USER_NAME() = '{user}'). Abbruch.");
            }
        }

        await using var command = new SqlCommand(_options.AnimalQuery, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds,
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var animals = HorizonAnimalMapper.Map(reader);
        _logger.LogInformation("Horizon gelesen: {Count} Tiere.", animals.Count);
        return animals;
    }
}
