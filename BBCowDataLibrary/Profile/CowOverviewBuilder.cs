using BB_Cow.Class;

namespace BB_Cow.Profile;

/// <summary>
/// Baut die Zeilen der Kuh-Uebersicht in einem Durchlauf je Behandlungsart.
/// </summary>
public static class CowOverviewBuilder
{
    /// <summary>
    /// Gruppiert ueber die Cow_ID, NIE ueber die Halsbandnummer: die wird nach
    /// einem Abgang neu vergeben, zwei Tiere teilen sie sich also ueber die
    /// Zeit (CowService.IsCollarInUse prueft nur die nicht abgegangenen).
    /// Und Behandlung.Ear_Tag_Number haelt die Cow_ID, nicht die Ohrmarke -
    /// siehe die Migration AddCowIdAndIsCalv.
    ///
    /// Behandlungen, deren Cow_ID zu keinem Tier gehoert, fallen heraus: sie
    /// liessen sich auf keiner Seite anzeigen.
    /// </summary>
    public static IReadOnlyList<CowOverviewRow> Build(
        IEnumerable<Cow> cows,
        IEnumerable<CowTreatment> cowTreatments,
        IEnumerable<ClawTreatment> clawTreatments)
    {
        var cowStats = Fold(cowTreatments.Select(t => (t.EarTagNumber, t.AdministrationDate)));
        var clawStats = Fold(clawTreatments.Select(t => (t.EarTagNumber, t.TreatmentDate)));

        return cows
            .Select(cow =>
            {
                cowStats.TryGetValue(cow.CowId, out var mine);
                clawStats.TryGetValue(cow.CowId, out var claw);

                return new CowOverviewRow(
                    cow.CowId,
                    cow.CollarNumber,
                    cow.EarTagNumber,
                    cow.IsCalv,
                    cow.IsGone,
                    mine.Count,
                    claw.Count,
                    Newer(mine.Last, claw.Last));
            })
            .ToList();
    }

    private static Dictionary<string, (int Count, DateTime? Last)> Fold(
        IEnumerable<(string CowId, DateTime Date)> treatments)
    {
        var stats = new Dictionary<string, (int Count, DateTime? Last)>(StringComparer.Ordinal);

        foreach (var (cowId, date) in treatments)
        {
            if (string.IsNullOrEmpty(cowId))
            {
                continue;
            }

            stats.TryGetValue(cowId, out var current);
            stats[cowId] = (
                current.Count + 1,
                current.Last is { } last && last >= date ? last : date);
        }

        return stats;
    }

    private static DateTime? Newer(DateTime? a, DateTime? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a > b ? a : b;
    }
}
