using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Die reinen Nachschlage- und Vorschlagsregeln ueber den
/// Behandlungsgrund-Cache. Rumpf hier, delegierende Zeile in
/// TreatmentReasonService.
/// </summary>
public static class TreatmentReasonLookups
{
    public static List<string> ReasonNames(IEnumerable<TreatmentReason> reasons)
    {
        return reasons.Select(r => r.TreatmentReasonName).Distinct().ToList();
    }

    /// <summary>
    /// Anzeigename. <paramref name="noReasonText"/> bei null und bei
    /// unbekannter ID.
    ///
    /// Der Platzhalter kommt als Parameter herein, statt hier als Konstante zu
    /// stehen: TreatmentReasonService.NoReasonText hat ausserhalb ihrer Klasse
    /// keine Aufrufstelle und bleibt deshalb dort. Eine zweite Konstante hier
    /// waere die Drift, die dieses Projekt verhindern soll.
    /// </summary>
    public static string GetNameById(IReadOnlyDictionary<int, TreatmentReason> reasons, int? id, string noReasonText)
    {
        if (id is not int value)
        {
            return noReasonText;
        }

        return reasons.TryGetValue(value, out var reason)
            ? reason.TreatmentReasonName
            : noReasonText;
    }

    /// <summary>
    /// Vorschlaege fuer das Autocomplete. Nach dem Muster von
    /// CowTreatmentService.SearchCowTreatmentWhereHow: eine Eingabe ohne
    /// Treffer liefert die Eingabe selbst zurueck, damit sie uebernommen und
    /// beim Speichern angelegt werden kann.
    ///
    /// Heisst Search und nicht SearchAsync: hier gibt es kein Task. Das
    /// Task.FromResult bleibt in der Instanzmethode, deren Signatur sich nicht
    /// aendern darf. Das CancellationToken faellt weg - der Rumpf hat es nie
    /// gelesen.
    /// </summary>
    public static IEnumerable<string> Search(IEnumerable<string> reasonNames, string value)
    {
        var names = reasonNames.OrderBy(n => n, StringComparer.CurrentCulture).ToList();

        if (string.IsNullOrWhiteSpace(value))
        {
            return names;
        }

        var hits = names
            .Where(n => n.Contains(value, StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        return hits.Count > 0 ? hits : new List<string> { value.Trim() };
    }
}
