namespace Meadow.Shared.Models;

/// <summary>
/// Die Auswahl fuer die Sammelplanung auf der Seite "Geplante Klauen
/// Behandlungen": welche Kuehe seit mindestens N Tagen keine Klauenbehandlung
/// hatten und deshalb eine geplante bekommen sollen.
///
/// Als reine Funktion hier statt im Dialog, damit die Grenzen pruefbar sind.
/// <paramref name="today"/> ist Parameter statt DateTime.Now im Rumpf - dieselbe
/// Begruendung wie bei <see cref="ClawTreatmentExtensions.IsBandageOverdue"/>.
/// </summary>
public static class ClawTreatmentPlanning
{
    /// <summary>
    /// Die Kuehe, fuer die eine Planung angelegt werden soll.
    ///
    /// - Kaelber nie: sie stehen noch ohne Ohrmarke im Bestand.
    /// - <paramref name="onlyActive"/>: abgegangene Tiere (IsGone) fallen weg.
    /// - Nie an den Klauen behandelt zaehlt als "laenger als N Tage".
    /// - Wer schon eine geplante Klauenbehandlung hat, bekommt keine zweite.
    ///
    /// EarTagNumber an den Behandlungen ist die Cow_ID, nicht die Ohrmarke.
    /// Verglichen wird auf Tagesebene: genau N Tage her zaehlt mit.
    /// </summary>
    public static IReadOnlyList<Cow> SelectCowsForPlanning(
        IEnumerable<Cow> cows,
        IEnumerable<ClawTreatment> treatments,
        IEnumerable<PlannedClawTreatment> planned,
        int daysWithout,
        bool onlyActive,
        DateTime today)
    {
        var lastTreatment = treatments
            .GroupBy(t => t.EarTagNumber)
            .ToDictionary(g => g.Key, g => g.Max(t => t.TreatmentDate).Date);

        var alreadyPlanned = planned.Select(p => p.EarTagNumber).ToHashSet();
        var cutoff = today.Date.AddDays(-daysWithout);

        return cows
            .Where(c => !c.IsCalv)
            .Where(c => !onlyActive || !c.IsGone)
            .Where(c => !alreadyPlanned.Contains(c.CowId))
            .Where(c => !lastTreatment.TryGetValue(c.CowId, out var last) || last <= cutoff)
            .OrderBy(c => c.CollarNumber)
            .ToList();
    }
}
