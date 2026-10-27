using Meadow.Shared.Lookups;

namespace Meadow.Shared.Models;

/// <summary>
/// Leitet Behandlungsplan-Durchlaeufe aus den Kuhbehandlungen ab. Rein: keine
/// Uhr, kein Netz, keine Datenbank - Client (Dashboard, Kuh-Seite, Hinweise)
/// und Server (Push) rufen dieselbe Funktion auf. Regeln: Spec
/// 2026-10-19-behandlungsplaene-design.md, Abschnitt "Fachliche Regeln".
/// </summary>
public static class TreatmentPlanRuns
{
    public static IReadOnlyList<TreatmentPlanRun> Build(
        IEnumerable<CowTreatment> treatments,
        IReadOnlyDictionary<int, Medicine> medicines,
        IEnumerable<TreatmentPlanCancellation> cancellations,
        DateTime today)
    {
        var day = today.Date;
        // Unique-Index auf Start_Treatment_Client_Id - doppelte gibt es nicht,
        // GroupBy schuetzt trotzdem vor einem Absturz bei kaputten Daten.
        var cancelByStart = cancellations
            .GroupBy(c => c.StartTreatmentClientId)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<TreatmentPlanRun>();
        var groups = treatments
            .Where(t => medicines.TryGetValue(t.MedicineId, out var m) && TreatmentPlanDefinition.IsDefined(m))
            .GroupBy(t => (CowId: t.EarTagNumber, t.MedicineId));

        foreach (var group in groups)
        {
            var medicine = medicines[group.Key.MedicineId];
            var validFrom = medicine.PlanValidFrom?.Date ?? DateTime.MinValue;
            var doses = group
                .Where(t => t.AdministrationDate.Date >= validFrom)
                .OrderBy(t => t.AdministrationDate)
                .ThenBy(t => t.CowTreatmentId);

            var current = new List<CowTreatment>();
            foreach (var dose in doses)
            {
                if (current.Count > 0 && !Accepts(current[0], dose, medicine, cancelByStart))
                {
                    result.Add(Close(current, medicine, cancelByStart, day, superseded: true));
                    current = new List<CowTreatment>();
                }

                current.Add(dose);

                if (current.Count == medicine.PlanDoseCount)
                {
                    result.Add(Close(current, medicine, cancelByStart, day, superseded: false));
                    current = new List<CowTreatment>();
                }
            }

            if (current.Count > 0)
            {
                result.Add(Close(current, medicine, cancelByStart, day, superseded: false));
            }
        }

        return result;
    }

    public static IReadOnlyList<TreatmentPlanRun> ForCow(IEnumerable<TreatmentPlanRun> runs, string cowId)
        => runs
            .Where(r => string.Equals(r.CowId, cowId, StringComparison.Ordinal))
            .OrderByDescending(r => r.IsActive)
            .ThenByDescending(r => r.StartDate)
            .ToList();

    public static IReadOnlyList<TreatmentPlanRun> ActiveForDashboard(
        IEnumerable<TreatmentPlanRun> runs, IReadOnlyDictionary<string, Cow> cows)
        => runs
            .Where(r => r.IsActive && CowLookups.GetById(cows, r.CowId) is { IsGone: false })
            .OrderBy(r => Urgency(r.Status))
            .ThenBy(r => r.NextDueDate ?? r.WindowEnd ?? DateTime.MaxValue)
            .ThenBy(r => r.CowId, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<TreatmentPlanRun> Due(
        IEnumerable<TreatmentPlanRun> runs, IReadOnlyDictionary<string, Cow> cows)
        => ActiveForDashboard(runs, cows).Where(r => r.IsDue).ToList();

    private static int Urgency(TreatmentPlanRunStatus status) => status switch
    {
        TreatmentPlanRunStatus.Overdue => 0,
        TreatmentPlanRunStatus.DueToday => 1,
        _ => 2
    };

    private static DateTime WindowEnd(CowTreatment first, Medicine medicine)
        => first.AdministrationDate.Date.AddDays(medicine.PlanDays!.Value - 1);

    /// <summary>
    /// Nimmt der offene Durchlauf (erste Gabe <paramref name="first"/>) diese Gabe an?
    /// Abgebrochen: nur bis einschliesslich Abbruchtag. Window: nur bis Fensterende.
    /// Interval: jede spaetere Gabe.
    /// </summary>
    private static bool Accepts(
        CowTreatment first, CowTreatment dose, Medicine medicine,
        IReadOnlyDictionary<Guid, TreatmentPlanCancellation> cancelByStart)
    {
        if (cancelByStart.TryGetValue(first.ClientId, out var cancellation)
            && dose.AdministrationDate.Date > cancellation.CancelledOn.Date)
        {
            return false;
        }

        return medicine.PlanMode != TreatmentPlanMode.Window
               || dose.AdministrationDate.Date <= WindowEnd(first, medicine);
    }

    private static TreatmentPlanRun Close(
        List<CowTreatment> doses, Medicine medicine,
        IReadOnlyDictionary<Guid, TreatmentPlanCancellation> cancelByStart,
        DateTime today, bool superseded)
    {
        var first = doses[0];
        var doseCount = medicine.PlanDoseCount!.Value;
        var days = medicine.PlanDays!.Value;
        var mode = medicine.PlanMode!.Value;
        cancelByStart.TryGetValue(first.ClientId, out var cancellation);

        DateTime? windowEnd = mode == TreatmentPlanMode.Window ? WindowEnd(first, medicine) : null;
        DateTime? nextDue = null;
        TreatmentPlanRunStatus status;

        if (doses.Count >= doseCount)
        {
            status = TreatmentPlanRunStatus.Completed;
        }
        else if (cancellation is not null)
        {
            status = TreatmentPlanRunStatus.Cancelled;
        }
        else if (superseded)
        {
            // Eine spaetere Gabe hat schon einen neuen Durchlauf begonnen -
            // dieser ist vorbei, egal was "heute" sagt (z.B. vordatierte Gabe).
            status = TreatmentPlanRunStatus.Incomplete;
        }
        else if (mode == TreatmentPlanMode.Window)
        {
            var remainingDoses = doseCount - doses.Count;
            var remainingDays = (windowEnd!.Value - today).Days + 1;
            var treatedToday = doses.Any(t => t.AdministrationDate.Date == today);

            status = remainingDays <= 0
                ? TreatmentPlanRunStatus.Incomplete
                : !treatedToday && remainingDoses >= remainingDays
                    ? TreatmentPlanRunStatus.DueToday
                    : TreatmentPlanRunStatus.Upcoming;
        }
        else
        {
            nextDue = doses[^1].AdministrationDate.Date.AddDays(days);
            status = today < nextDue ? TreatmentPlanRunStatus.Upcoming
                : today == nextDue ? TreatmentPlanRunStatus.DueToday
                : TreatmentPlanRunStatus.Overdue;
        }

        return new TreatmentPlanRun
        {
            CowId = first.EarTagNumber,
            MedicineId = first.MedicineId,
            StartTreatmentClientId = first.ClientId,
            Mode = mode,
            DoseCount = doseCount,
            PlanDays = days,
            Doses = doses.ToList(),
            Status = status,
            NextDueDate = nextDue,
            WindowEnd = windowEnd,
            Cancellation = cancellation
        };
    }
}
