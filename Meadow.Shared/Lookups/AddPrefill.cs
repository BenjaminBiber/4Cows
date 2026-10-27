using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Was der Plus-Knopf einer Behandlungstabelle aus der aktuellen Suche in den
/// Hinzufuegen-Dialog uebernimmt.
///
/// <see cref="CowId"/> gesetzt: das Tier ist bekannt, der Dialog belegt
/// Halsband und Kuh/Kalb vor. Nur <see cref="CollarNumber"/>: die Nummer
/// gehoert zu keinem Tier im Bestand - sie steht trotzdem im Halsbandfeld,
/// und der Dialog meldet beim Speichern wie gewohnt "nicht gefunden".
/// </summary>
public sealed record AddPrefill(string? CowId, int? CollarNumber)
{
    // Wie TableSearch.EarTagMinDigits im Client: bis drei Ziffern ist eine
    // reine Ziffernsuche eine Halsbandnummer, ab vier eine Ohrmarke.
    private const int MaxCollarDigits = 3;

    /// <summary>
    /// Leitet die Vorbelegung aus Suchtext und Treffern ab.
    ///
    /// - Alle Treffer gehoeren zu EINEM Tier, und die Suche meint dieses Tier
    ///   (Halsbandnummer oder Teil der Ohrmarke): dieses Tier. Eine Freitext-
    ///   Suche nach "Mortellaro", die zufaellig nur eine Kuh trifft, belegt
    ///   nichts vor.
    /// - Keine Treffer und eine Halsbandnummer gesucht: die Nummer, und das
    ///   Tier dazu, falls es eines im Bestand gibt (z. B. ohne bisherige
    ///   Behandlung).
    /// - Keine Treffer und eine Ohrmarke gesucht: das eine Tier im Bestand,
    ///   dessen Ohrmarke passt.
    /// - Sonst (leere Suche, mehrere Tiere, nichts Eindeutiges): null.
    /// </summary>
    /// <param name="matchedCowIds">Die Cow_IDs der aktuell angezeigten Zeilen.</param>
    public static AddPrefill? FromSearch(
        string? search,
        IEnumerable<string?> matchedCowIds,
        IReadOnlyDictionary<string, Cow> cows)
    {
        var s = search?.Trim() ?? "";
        if (s.Length == 0)
        {
            return null;
        }

        var ids = matchedCowIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();

        if (ids.Count > 1)
        {
            return null;
        }

        if (ids.Count == 1)
        {
            return cows.TryGetValue(ids[0]!, out var cow) && Identifies(cow, s)
                ? ForCow(cow)
                : null;
        }

        if (IsCollarSearch(s, out var collar))
        {
            var cowId = CowLookups.GetCowIdByCollarNumber(cows, collar);
            return new AddPrefill(cowId.Length > 0 ? cowId : null, collar);
        }

        var tagged = cows.Values
            .Where(c => !c.IsGone && EarTagContains(c, s))
            .Take(2)
            .ToList();

        return tagged.Count == 1 ? ForCow(tagged[0]) : null;
    }

    private static AddPrefill ForCow(Cow cow)
        => new(cow.CowId, cow.CollarNumber > 0 ? cow.CollarNumber : null);

    private static bool Identifies(Cow cow, string search)
        => IsCollarSearch(search, out var collar)
            ? cow.CollarNumber == collar
            : EarTagContains(cow, search);

    private static bool IsCollarSearch(string search, out int collar)
    {
        collar = 0;
        return search.Length <= MaxCollarDigits
               && search.All(char.IsDigit)
               && int.TryParse(search, out collar)
               && collar > 0;
    }

    // Leerzeichen raus, damit "08 1523" auch "DE 0815 23..." trifft und
    // umgekehrt - Ohrmarken werden mal mit, mal ohne Gruppierung getippt.
    private static bool EarTagContains(Cow cow, string search)
    {
        if (string.IsNullOrEmpty(cow.EarTagNumber))
        {
            return false;
        }

        var needle = Compact(search);
        return needle.Length > 0
               && Compact(cow.EarTagNumber).Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static string Compact(string value) => new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
}
