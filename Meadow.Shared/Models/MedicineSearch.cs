namespace BB_Cow.Class;

/// <summary>
/// Reihenfolge der Vorschlaege im Medikamenten-Autocomplete.
///
/// Vorher gab die Suche bei leerer Eingabe die ganze Liste alphabetisch
/// zurueck. Weil MudAutocomplete ohnehin nur die ersten Treffer anzeigt,
/// entscheidet die Sortierung darueber, ob im Stall das gesuchte Praeparat
/// oben steht oder ein alphabetisch frueheres.
///
/// Reine Statics, damit die Rangfolge ohne Datenbank pruefbar ist.
/// </summary>
public static class MedicineSearch
{
    /// <summary>
    /// Obergrenze der zurueckgegebenen Vorschlaege. Grosszuegiger als die
    /// Anzeige des Autocomplete, damit dessen eigene Begrenzung noch etwas zu
    /// waehlen hat - aber klein genug, dass kein Tastendruck die ganze Liste
    /// sortiert.
    /// </summary>
    public const int DefaultLimit = 50;

    /// <summary>
    /// Passende Namen, beste zuerst. Die Rangfolge:
    ///
    /// 1. <b>Treffer am Wortanfang</b> vor Treffer irgendwo im Namen. Wer
    ///    "ubro" tippt, meint "Ubrolexin" und nicht ein Praeparat, das die
    ///    Zeichenfolge in der Mitte traegt.
    /// 2. <b>Kuerzere Namen zuerst.</b> "Ubrolexin" vor "Ubrolexin 100 mg/ml
    ///    Injektionssuspension fuer Rinder" - beides dasselbe Praeparat, das
    ///    kuerzere ist im Stall das brauchbarere.
    /// 3. Alphabetisch, damit die Reihenfolge bei gleichem Rang stabil ist.
    /// </summary>
    public static IEnumerable<string> Rank(
        IEnumerable<Medicine> medicines, string? term, int limit = DefaultLimit)
    {
        var text = term?.Trim() ?? "";

        var matches = text.Length == 0
            ? medicines
            : medicines.Where(m => m.MedicineName.Contains(text, StringComparison.OrdinalIgnoreCase));

        return matches
            .OrderBy(m => text.Length > 0
                          && m.MedicineName.TrimStart()
                              .StartsWith(text, StringComparison.OrdinalIgnoreCase)
                ? 0
                : 1)
            .ThenBy(m => m.MedicineName.Length)
            .ThenBy(m => m.MedicineName, StringComparer.CurrentCultureIgnoreCase)
            .Select(m => m.MedicineName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, limit));
    }
}
