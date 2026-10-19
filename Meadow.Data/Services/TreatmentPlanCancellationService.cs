using System.Collections.Immutable;
using Meadow.Data.Sql;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Data.Services;

/// <summary>
/// Die abgebrochenen Behandlungsplan-Durchlaeufe. Aufgebaut wie
/// <see cref="TreatmentReasonService"/>: Singleton mit prozessweitem Cache,
/// geschrieben wird ueber die Kuh-Seite.
///
/// Der Cache steht auf der StartTreatmentClientId und nicht auf dem
/// Primaerschluessel: gefragt wird immer "ist DIESER Durchlauf abgebrochen",
/// nie "zeig mir Abbruch 17".
/// </summary>
public class TreatmentPlanCancellationService : ITreatmentPlanCancellationService
{
    private ImmutableDictionary<Guid, TreatmentPlanCancellation> _cachedCancellations =
        ImmutableDictionary<Guid, TreatmentPlanCancellation>.Empty;

    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;

    public ImmutableDictionary<Guid, TreatmentPlanCancellation> Cancellations => _cachedCancellations;

    public TreatmentPlanCancellationService(
        IDbContextFactory<DatabaseContext> contextFactory,
        DatabaseStatusService databaseStatusService)
    {
        _contextFactory = contextFactory;
        _databaseStatusService = databaseStatusService;
    }

    public async Task GetAllDataAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var cancellations = await context.TreatmentPlanCancellations.AsNoTracking().ToListAsync();
            _cachedCancellations = cancellations.ToImmutableDictionary(c => c.StartTreatmentClientId);
            _databaseStatusService.ReportSuccess();
            LoggerService.LogInformation(typeof(TreatmentPlanCancellationService),
                $"Loaded {_cachedCancellations.Count} treatment plan cancellations.");
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(TreatmentPlanCancellationService),
                "Failed to load treatment plan cancellations, with {@Message}", ex, ex.Message);
        }
    }

    /// <summary>
    /// Bricht einen Durchlauf ab. Idempotent: liegt fuer die Start-ClientId
    /// schon ein Abbruch vor, bleibt der STEHENDE erhalten und es wird nichts
    /// geschrieben - auch nicht der neue Grund. Ein zweites Tippen auf
    /// "Abbrechen" darf den ersten Eintrag nicht umdatieren.
    ///
    /// Geprueft wird zweimal: im Cache (der schnelle Weg) und in der Datenbank
    /// (der verlaessliche - der Cache ist ein Singleton und kann aelter sein als
    /// die Tabelle). Die eigentliche Garantie ist trotzdem der eindeutige Index;
    /// zwei gleichzeitige Aufrufer kommen beide an beiden Pruefungen vorbei und
    /// genau einer verliert dort. Das ist kein Fehler, sondern das gewuenschte
    /// Ergebnis - deshalb der Nachschlag im catch-Zweig, wie in
    /// ClientIdUpsert.RaceWinnerAsync.
    /// </summary>
    public async Task<bool> CancelAsync(TreatmentPlanCancellation cancellation)
    {
        if (cancellation.StartTreatmentClientId == Guid.Empty)
        {
            return false;
        }

        if (_cachedCancellations.ContainsKey(cancellation.StartTreatmentClientId))
        {
            return true;
        }

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.TreatmentPlanCancellations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.StartTreatmentClientId == cancellation.StartTreatmentClientId);

            if (existing is not null)
            {
                _cachedCancellations = _cachedCancellations.SetItem(existing.StartTreatmentClientId, existing);
                _databaseStatusService.ReportSuccess();
                return true;
            }

            // Der Abbruch gilt fuer einen TAG, nicht fuer eine Uhrzeit - genau
            // wie jeder andere Datumsvergleich dieser Anwendung auf .Date geht.
            cancellation.CancelledOn = cancellation.CancelledOn.Date;

            // Lieber gekuerzt als abgelehnt: der Grund ist ein Freitextfeld und
            // nebensaechlich gegenueber der Tatsache, DASS abgebrochen wurde.
            if (cancellation.Reason is { Length: > TreatmentPlanCancellation.ReasonMaxLength })
            {
                cancellation.Reason = cancellation.Reason[..TreatmentPlanCancellation.ReasonMaxLength];
            }

            await context.TreatmentPlanCancellations.AddAsync(cancellation);
            await context.SaveChangesAsync();
            _databaseStatusService.ReportSuccess();

            _cachedCancellations = _cachedCancellations.SetItem(cancellation.StartTreatmentClientId, cancellation);
            LoggerService.LogInformation(typeof(TreatmentPlanCancellationService),
                $"Cancelled treatment plan run {cancellation.StartTreatmentClientId}.");

            return true;
        }
        catch (Exception ex)
        {
            // Hat ein anderer Aufrufer das Rennen am eindeutigen Index gewonnen,
            // steht die Zeile jetzt da - und dann war der Aufruf erfolgreich.
            if (await ReloadAndFindAsync(cancellation.StartTreatmentClientId))
            {
                return true;
            }

            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(TreatmentPlanCancellationService),
                "Failed to cancel treatment plan run, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Nimmt einen Abbruch zurueck. Idempotent in die andere Richtung: ist
    /// nichts da, ist nichts zu tun - und der Aufrufer wollte genau diesen
    /// Zustand.
    /// </summary>
    public async Task<bool> RevokeAsync(Guid startTreatmentClientId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var affectedRows = await context.TreatmentPlanCancellations
                .Where(c => c.StartTreatmentClientId == startTreatmentClientId)
                .ExecuteDeleteAsync();

            _databaseStatusService.ReportSuccess();
            _cachedCancellations = _cachedCancellations.Remove(startTreatmentClientId);

            if (affectedRows > 0)
            {
                LoggerService.LogInformation(typeof(TreatmentPlanCancellationService),
                    $"Revoked cancellation of treatment plan run {startTreatmentClientId}.");
            }

            return true;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(TreatmentPlanCancellationService),
                "Failed to revoke treatment plan cancellation, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Nach einem gescheiterten Insert nachsehen, ob die Zeile inzwischen doch
    /// da ist. Eigener Kontext, weil der des Aufrufers nach dem Fehlschlag die
    /// nicht gespeicherte Entitaet noch verfolgt.
    /// </summary>
    private async Task<bool> ReloadAndFindAsync(Guid startTreatmentClientId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var winner = await context.TreatmentPlanCancellations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.StartTreatmentClientId == startTreatmentClientId);

            if (winner is null)
            {
                return false;
            }

            _cachedCancellations = _cachedCancellations.SetItem(startTreatmentClientId, winner);
            return true;
        }
        catch
        {
            // Der eigentliche Fehler wird beim Aufrufer protokolliert; dieser
            // Nachschlag ist nur die Zweitmeinung und darf stumm scheitern.
            return false;
        }
    }
}
