using Meadow.Shared.Models;

namespace Meadow.Shared.Mcp;

/// <summary>
/// Wie die MCP-Werkzeuge aus dem, was Claude schreibt, eine Zeile machen.
///
/// Claude kennt keine Ids, sondern Ohrmarken, Halsbandnummern und
/// Medikamentnamen. Die Aufloesung ist STRENG: ein unbekannter Name ist ein
/// Fehler, kein stilles Anlegen - anders als im Behandlungs-Dialog, wo ein
/// getipptes Praeparat beim Speichern mit angelegt wird. Ein Sprachmodell,
/// das sich bei "Penicilin" vertippt, soll eine Rueckfrage bekommen und
/// keinen neuen Stammdatensatz.
/// </summary>
public static class McpLookup
{
    /// <summary>
    /// Kuh nach Cow-ID, Ohrmarke oder Halsbandnummer, in dieser Reihenfolge.
    ///
    /// Die Halsbandnummer zuletzt und nur unter den nicht abgegangenen Tieren:
    /// sie wird nach einem Abgang neu vergeben, die Ohrmarke nicht. Eine reine
    /// Zahl kann auch eine Ohrmarke sein - deshalb erst die exakten Treffer.
    /// </summary>
    public static Cow? ResolveCow(IReadOnlyDictionary<string, Cow> cows, string? query)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q))
        {
            return null;
        }

        if (cows.TryGetValue(q, out var byId))
        {
            return byId;
        }

        var byTag = cows.Values.FirstOrDefault(c =>
            string.Equals(c.EarTagNumber, q, StringComparison.OrdinalIgnoreCase));
        if (byTag is not null)
        {
            return byTag;
        }

        return int.TryParse(q, out var collar)
            ? cows.Values.FirstOrDefault(c => c.CollarNumber == collar && !c.IsGone)
            : null;
    }

    /// <summary>
    /// Ein Stammdatensatz per Id (als Zahl) oder Name, Name ohne Ruecksicht
    /// auf Gross-/Kleinschreibung und Leerzeichen am Rand - so, wie die
    /// Dienste selbst vergleichen.
    /// </summary>
    public static int? ResolveId<T>(
        IReadOnlyDictionary<int, T> items,
        string? query,
        Func<T, string?> name)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q))
        {
            return null;
        }

        if (int.TryParse(q, out var id) && items.ContainsKey(id))
        {
            return id;
        }

        foreach (var (key, value) in items)
        {
            if (string.Equals(name(value)?.Trim(), q, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>
    /// Klauen- bzw. Viertelangabe wie "LV, RH" in die vier Kennzeichen.
    /// Unbekannte Kuerzel liefern null, damit das Werkzeug einen Fehler melden
    /// kann statt eine Klaue still zu verschlucken.
    /// </summary>
    public static IReadOnlySet<HoofPosition>? ParsePositions(string? positions)
    {
        var result = new HashSet<HoofPosition>();
        if (string.IsNullOrWhiteSpace(positions))
        {
            return result;
        }

        foreach (var part in positions.Split(new[] { ',', ';', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            // Nur Buchstaben: Enum.TryParse nimmt sonst auch "1" und macht RV daraus.
            if (!part.All(char.IsLetter)
                || !Enum.TryParse<HoofPosition>(part, ignoreCase: true, out var position)
                || !Enum.IsDefined(position))
            {
                return null;
            }

            result.Add(position);
        }

        return result;
    }
}
