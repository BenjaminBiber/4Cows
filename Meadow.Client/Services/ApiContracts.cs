// Die Anfrage- und Antwortrumpfe der Endpunkte, die kein Modell aus
// Meadow.Shared uebertragen.
//
// Als eigene Typen und nicht als anonyme Objekte: ein anonymes Objekt liest
// sich beim Schreiben bequem, aber wenn der Endpunkt sein Feld umbenennt,
// faellt das erst zur Laufzeit auf - und zwar als still ignoriertes Feld, nicht
// als Fehler. Hier stehen sie einmal, gegenueber der Datei, aus der sie
// stammen.
//
// Die Namen spiegeln die Records in Meadow.Api/Endpoints. Geteilt werden sie
// NICHT: Meadow.Api referenziert die EF-Schicht, und die Naht laeuft ausdruecklich
// nur ueber Meadow.Shared.

namespace Meadow.Client.Services;

// ---- Anfragen -------------------------------------------------------------

/// <summary>Rumpf von PUT /api/cows/{cowId}/collar-number.</summary>
internal sealed record CollarNumberUpdate(int CollarNumber);

/// <summary>Rumpf von PUT /api/cows/{cowId}/is-gone.</summary>
internal sealed record IsGoneUpdate(bool IsGone);

/// <summary>Rumpf von PUT /api/cows/{cowId}/ear-tag.</summary>
internal sealed record EarTagAssignment(string? EarTagNumber);

/// <summary>
/// Rumpf der drei namensgleichen Upserts: /api/medicines/by-name,
/// /api/treatment-reasons/by-name und /api/claw-findings/by-name.
/// </summary>
internal sealed record NameRequest(string? Name);

/// <summary>
/// Rumpf von POST /api/where-hows/by-name. ShowDialog ist NULLABLE, weil ein
/// fehlendes Feld den Vorgabewert des Dienstes (true) gelten lassen soll - ein
/// nicht gesetztes bool waere still false.
/// </summary>
internal sealed record WhereHowByNameRequest(string? Name, bool? ShowDialog);

/// <summary>
/// Rumpf der Merge-Endpunkte von Medikament und Klauenbefund. Beide Dienste
/// kennen einen ueberlebenden Namen.
/// </summary>
internal sealed record NamedMergeRequest(int TargetId, string? SurvivingName);

/// <summary>
/// Rumpf der Merge-Endpunkte von Wie/Wo und Behandlungsgrund. Diese beiden
/// Dienste kennen KEINEN ueberlebenden Namen - ihre MergeAsync nimmt nur zwei
/// Ids.
/// </summary>
internal sealed record MergeRequest(int TargetId);

/// <summary>Rumpf von POST /api/udders/by-quarters.</summary>
internal sealed record UdderByQuartersRequest(bool QuarterLV, bool QuarterLH, bool QuarterRV, bool QuarterRH);

/// <summary>Rumpf von POST /api/claw-treatments/bandages-removed.</summary>
internal sealed record BandageRemovalRequest(IReadOnlyCollection<int> Ids);

/// <summary>Rumpf von PUT /api/settings/{key}.</summary>
internal sealed record SettingValueUpdate(string Value);

// ---- Antworten ------------------------------------------------------------

/// <summary>
/// Antwort der fuenf Upserts. Id ist NULLABLE, weil /api/claw-findings/by-name
/// fuer eine leere Eingabe eine 200 mit {"id":null} liefert - "an dieser Klaue
/// wurde nichts erfasst" ist dort der regulaere Fall und kein Fehler.
/// </summary>
internal sealed record IdResponse(int? Id);

/// <summary>Antwort von POST /api/claw-treatments/bandages-removed.</summary>
internal sealed record RemovedResponse(int Removed);

/// <summary>Antwort von POST /api/kpi/{kpiId}/value.</summary>
internal sealed record KpiValueResponse(int KpiId, string? Value);

/// <summary>
/// Antwort von GET /api/xlink/status. Die Felder intervalHours und url bleiben
/// hier weg - sie gehoeren zum Infrastruktur-Dialog und nicht zur Naht.
/// </summary>
internal sealed record XLinkStatusResponse(
    DateTimeOffset? LastSyncUtc,
    bool LastSyncSucceeded,
    string? LastSyncError,
    bool Running,
    bool Enabled);
