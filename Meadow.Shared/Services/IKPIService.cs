using System.Collections.Immutable;
using Meadow.Shared.Kpi;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die Kennzahlen.
///
/// GetKPIValueAsync(DatabaseContext, KPI, bool) steht bewusst NICHT hier: sie
/// nimmt einen DatabaseContext, weil sie sich eine Verbindung ueber alle
/// SQL-Kennzahlen eines Dashboards teilt. Ein DatabaseContext ist EF, und EF
/// gehoert nicht in dieses Projekt - der Browser laedt es sonst mit. Die
/// Methode bleibt oeffentlich am EF-Dienst; ueber die Naht laeuft
/// GetDashboardAsync, die dieselbe Ersparnis von innen loest.
/// </summary>
public interface IKPIService
{
    ImmutableDictionary<int, KPI> KPIs { get; }

    Task GetAllDataAsync();

    Task<bool> InsertDataAsync(KPI KPI);

    Task<string> GetKPIValue(KPI kpi, bool throwError = false);

    /// <summary>
    /// Prüft ein Skript, OHNE es zu speichern.
    ///
    /// Existiert, weil der bisherige "SQL testen"-Knopf die gespeicherte Zeile prüfte und bei einer
    /// neuen Kennzahl deshalb gar nicht funktionieren konnte - die Zeile gab es noch nicht.
    /// </summary>
    Task<KpiScriptCheck> CheckScriptAsync(string? script);

    Task<IReadOnlyList<KpiTileModel>> GetDashboardAsync(bool addButtonKPI = true);

    Task<bool> UpdateDataAsync(KPI KPI);

    Task<bool> DeleteDataAsync(int kpiId);
}
