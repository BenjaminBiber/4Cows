using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Die reinen Nachschlage- und Vorschlagsregeln ueber den Klauenbefund-Cache.
/// Rumpf hier, delegierende Zeile in ClawFindingService.
/// </summary>
public static class ClawFindingLookups
{
    public static List<string> FindingNames(IEnumerable<ClawFinding> findings)
    {
        return findings.Select(f => f.ClawFindingName).Distinct().ToList();
    }

    /// <summary>
    /// Anzeigename. Leerstring bei <c>null</c> UND bei unbekannter ID - nicht
    /// der Gedankenstrich der uebrigen Nachschlagedienste.
    ///
    /// Leer heisst in der ganzen Klauen-Anzeige "an dieser Klaue nichts
    /// erfasst": ClawFindingSummary, CowProfileBuilder und ClawSummary haengen
    /// daran. Ein Platzhalterzeichen hier wuerde als echter Befund gezaehlt und
    /// stuende als haeufigster Klauenbefund auf der Kachel.
    /// </summary>
    public static string GetNameById(IReadOnlyDictionary<int, ClawFinding> findings, int? id)
    {
        if (id is not int value)
        {
            return string.Empty;
        }

        return findings.TryGetValue(value, out var finding)
            ? finding.ClawFindingName
            : string.Empty;
    }

    /// <summary>
    /// Vorschlaege fuer das Autocomplete. Nach dem Muster von
    /// TreatmentReasonService.SearchAsync: eine Eingabe ohne Treffer liefert
    /// die Eingabe selbst zurueck, damit sie uebernommen und beim Speichern
    /// angelegt werden kann.
    ///
    /// Heisst Search und nicht SearchAsync: hier gibt es kein Task. Das
    /// Task.FromResult bleibt in der Instanzmethode, deren Signatur sich nicht
    /// aendern darf. Das CancellationToken faellt weg - der Rumpf hat es nie
    /// gelesen.
    /// </summary>
    public static IEnumerable<string> Search(IEnumerable<string> findingNames, string value)
    {
        var names = findingNames.OrderBy(n => n, StringComparer.CurrentCulture).ToList();

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
