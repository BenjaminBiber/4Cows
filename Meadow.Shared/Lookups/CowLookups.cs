using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Die reinen Nachschlage- und Anzeigeregeln rund um den Kuh-Cache.
///
/// Hier steht der Rumpf, in CowService nur noch die delegierende Zeile. Das
/// ist Absicht: EF-Dienst und spaeterer HTTP-Dienst teilen sich genau diese
/// eine Kopie und koennen sich deshalb nicht darueber uneinig werden, was
/// "Kalb" heisst oder welche Kuh zu einer Halsbandnummer gehoert.
///
/// Der Cache kommt als schmalstes lesendes Interface herein, nie der Dienst -
/// sonst haette dieses Projekt wieder eine Abhaengigkeit auf die Naht, die es
/// bedienen soll.
/// </summary>
public static class CowLookups
{
    public static Cow GetById(IReadOnlyDictionary<string, Cow> cows, string cowId)
    {
        return cows.ContainsKey(cowId) ? cows[cowId] : null;
    }

    // Finds a cow by its ear tag FIELD (not the Cow_ID key). Needed because a promoted calf
    // keeps its GUID Cow_ID while gaining an ear tag, so ear-tag lookups can't go through the key.
    public static Cow GetByEarTagNumber(IReadOnlyDictionary<string, Cow> cows, string earTagNumber)
    {
        if (string.IsNullOrWhiteSpace(earTagNumber))
        {
            return null;
        }
        return cows.Values.FirstOrDefault(c => c.EarTagNumber == earTagNumber);
    }

    public static string GetEarTagNumberByCollarNumber(IReadOnlyDictionary<string, Cow> cows, int collarNumber, bool searchContainsLeavage = true)
    {
        if(searchContainsLeavage)
        {
            return cows.Values.FirstOrDefault(c => c.CollarNumber == collarNumber)?.EarTagNumber ?? String.Empty;
        }
        else
        {
            return cows.Values.FirstOrDefault(c => c.CollarNumber == collarNumber && !c.IsGone)?.EarTagNumber ?? String.Empty;
        }
    }

    // Resolves a collar number to the cow's stable Cow_ID. Works for calves (no ear tag) too,
    // which is why treatment dialogs use this instead of GetEarTagNumberByCollarNumber.
    public static string GetCowIdByCollarNumber(IReadOnlyDictionary<string, Cow> cows, int collarNumber, bool includeGone = false)
    {
        var match = includeGone
            ? cows.Values.FirstOrDefault(c => c.CollarNumber == collarNumber)
            : cows.Values.FirstOrDefault(c => c.CollarNumber == collarNumber && !c.IsGone);
        return match?.CowId ?? String.Empty;
    }

    public static int GetCollarNumberByCowId(IReadOnlyDictionary<string, Cow> cows, string cowId)
    {
        return !string.IsNullOrEmpty(cowId) && cows.ContainsKey(cowId) ? cows[cowId].CollarNumber : int.MinValue;
    }

    // The non-gone calf (no ear tag) that carries this collar number, if any.
    public static Cow GetCalfByCollarNumber(IReadOnlyDictionary<string, Cow> cows, int collarNumber)
    {
        return cows.Values.FirstOrDefault(c => c.IsCalv && !c.IsGone
            && string.IsNullOrWhiteSpace(c.EarTagNumber) && c.CollarNumber == collarNumber);
    }

    public static bool IsCollarInUse(IReadOnlyDictionary<string, Cow> cows, int collarNumber)
    {
        return cows.Values.Any(c => !c.IsGone && c.CollarNumber == collarNumber);
    }

    // Autocomplete search for the treatment dialogs. Matches on collar number, ear tag OR Cow_ID,
    // and returns Cow_IDs (the value the treatment stores). Excludes cows that left the farm.
    //
    // Synchron, obwohl CowService.SearchCows ein Task zurueckgibt: die Rechnung hier ist es auch.
    // Das Task.FromResult bleibt in der Instanzmethode, damit deren Signatur sich nicht aendert.
    // Das CancellationToken faellt weg - der Rumpf hat es nie gelesen.
    public static IEnumerable<string> SearchCows(IReadOnlyDictionary<string, Cow> cows, string value)
    {
        IEnumerable<Cow> query = cows.Values.Where(c => !c.IsGone);
        if (!string.IsNullOrEmpty(value))
        {
            query = query.Where(c =>
                (c.EarTagNumber != null && c.EarTagNumber.Contains(value, StringComparison.InvariantCultureIgnoreCase))
                || c.CollarNumber.ToString().Contains(value)
                || c.CowId.Contains(value, StringComparison.InvariantCultureIgnoreCase));
        }

        return query.OrderBy(c => c.CollarNumber).Select(c => c.CowId);
    }

    // Ear-tag-only display for a Cow_ID: the real ear tag for identified cows, "Kalb" for a
    // calf (or an unknown/removed cow). Used by the "Ohrmarkennummer" table columns so a calf's
    // raw Cow_ID (a GUID) is never shown.
    public static string GetEarTagDisplay(IReadOnlyDictionary<string, Cow> cows, string cowId)
    {
        if (!string.IsNullOrEmpty(cowId) && cows.TryGetValue(cowId, out var cow)
            && !string.IsNullOrWhiteSpace(cow.EarTagNumber))
        {
            return cow.EarTagNumber;
        }
        return "Kalb";
    }

    // Ohrmarken-Label fuer einen Cow_ID in den Behandlungsdialogen. Die Halsbandnummer steht
    // direkt daneben in ihrem eigenen Feld und wird hier deshalb nicht wiederholt. Ein Kalb
    // hat keine Ohrenmarkennummer, dort bleibt die Halsbandnummer als einziges Merkmal.
    public static string GetDisplayLabel(IReadOnlyDictionary<string, Cow> cows, string? cowId)
    {
        if (string.IsNullOrEmpty(cowId) || !cows.ContainsKey(cowId))
        {
            return cowId ?? string.Empty;
        }

        var cow = cows[cowId];
        if (cow.IsCalv || string.IsNullOrWhiteSpace(cow.EarTagNumber))
        {
            return $"Kalb ({cow.CollarNumber})";
        }

        return cow.EarTagNumber;
    }

    public static bool FilterFuncCow(IReadOnlyDictionary<string, Cow> cows, string cowId, string searchString)
    {
        if (!cows.ContainsKey(cowId))
        {
            return false;
        }
        var cow = cows[cowId];
        if (string.IsNullOrWhiteSpace(searchString))
            return true;
        var search = searchString.ToLower();
        var collar = cow.CollarNumber.ToString().ToLower();
        var earTag = cow.EarTagNumber?.ToLower() ?? string.Empty;
        if (searchString.Length < 3 && search == collar)
        {
            return true;
        }
        if ((searchString.Length >= 3 && earTag.Contains(search)) || collar == search)
        {
            return true;
        }
        return false;
    }
}
