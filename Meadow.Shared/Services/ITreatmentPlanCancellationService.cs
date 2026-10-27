using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die abgebrochenen Behandlungsplan-Durchlaeufe.
///
/// Anders als die dreizehn Stammdaten-Dienste kennt sie kein Aendern: ein
/// Abbruch ist entweder da oder nicht. Deshalb nur Anlegen (idempotent) und
/// Zuruecknehmen - und beides ist nur online moeglich, der Abbruch wandert
/// NICHT in die Outbox.
/// </summary>
public interface ITreatmentPlanCancellationService
{
    /// <summary>Cache, Schluessel = StartTreatmentClientId.</summary>
    ImmutableDictionary<Guid, TreatmentPlanCancellation> Cancellations { get; }

    Task GetAllDataAsync();

    /// <summary>Idempotent: existiert fuer die Start-ClientId schon ein Abbruch, bleibt er stehen.</summary>
    Task<bool> CancelAsync(TreatmentPlanCancellation cancellation);

    /// <summary>Abbruch zuruecknehmen. Nicht vorhanden ist kein Fehler.</summary>
    Task<bool> RevokeAsync(Guid startTreatmentClientId);
}
