using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Wortlaut der Behandlungsplan-Erinnerungen - dieselbe Quelle fuer den
/// In-App-Hinweis (Client) und die taegliche Sammel-Push (Server).
/// Dringlichkeit zuerst, dann das Tier - wie beim Verband.
///
/// Rein und ohne Uhr: "heute" ist Parameter, damit die Tagesangabe pruefbar
/// bleibt und auf einem Geraet mit anderer Zeitzone dieselbe ist.
/// </summary>
public static class TreatmentPlanReminderText
{
    public const string PushTitle = "Behandlungspläne";

    /// <summary>
    /// Der Hinweistext zu EINEM faelligen Durchlauf. Die Faelligkeit selbst
    /// entscheidet <see cref="TreatmentPlanRuns"/> - hier wird nur formuliert.
    /// </summary>
    public static string For(TreatmentPlanRun run, string medicineName, IReadOnlyDictionary<string, Cow> cows, DateTime today)
    {
        var dose = $"Gabe {run.DosesDone + 1}/{run.DoseCount} {medicineName}";
        var when = run.Status == TreatmentPlanRunStatus.Overdue
            ? $"seit {Days(run.OverdueDays(today))} überfällig"
            : "heute fällig";
        return $"{dose} {when}: {run.CowId}" + CowLookups.CollarSuffix(cows, run.CowId);
    }

    /// <summary>
    /// Eine Zeile fuer die taegliche Sammel-Push; <c>null</c>, wenn nichts
    /// faellig ist (dann wird gar nicht gesendet). Gezaehlt werden KUEHE, nicht
    /// Durchlaeufe - zwei Plaene an derselben Kuh sind ein Gang in den Stall.
    /// Eine Kuh mit einem ueberfaelligen und einem heute faelligen Plan zaehlt
    /// nur als ueberfaellig: der dringendere Status gewinnt.
    /// </summary>
    public static string? PushSummary(IReadOnlyCollection<TreatmentPlanRun> dueRuns)
    {
        var overdueCows = dueRuns.Where(r => r.Status == TreatmentPlanRunStatus.Overdue)
            .Select(r => r.CowId).ToHashSet(StringComparer.Ordinal);
        var dueTodayCows = dueRuns.Where(r => r.Status == TreatmentPlanRunStatus.DueToday)
            .Select(r => r.CowId).Where(c => !overdueCows.Contains(c)).ToHashSet(StringComparer.Ordinal);

        var parts = new List<string>();
        if (dueTodayCows.Count > 0)
        {
            parts.Add($"{Cows(dueTodayCows.Count)} heute fällig");
        }

        if (overdueCows.Count > 0)
        {
            parts.Add($"{Cows(overdueCows.Count)} überfällig");
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string Days(int n) => n == 1 ? "1 Tag" : $"{n} Tagen";

    private static string Cows(int n) => n == 1 ? "1 Kuh" : $"{n} Kühe";
}
