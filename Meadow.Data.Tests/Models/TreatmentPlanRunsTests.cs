using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Models;

/// <summary>
/// Der Berechnungskern der Behandlungsplaene. Durchlaeufe werden nicht
/// gespeichert, sondern aus den Kuhbehandlungen abgeleitet - jede Regel des
/// Spec ("Fachliche Regeln") hat hier mindestens einen Fall.
/// </summary>
public class TreatmentPlanRunsTests
{
    private static readonly DateTime Today = new(2026, 6, 15);
    private const string CowA = "DE01";
    private const string CowB = "DE02";

    private int _id;

    private static Medicine Med(int id, int doses, TreatmentPlanMode mode, int days, DateTime? validFrom = null)
        => new(id, $"Med{id}") { PlanDoseCount = doses, PlanMode = mode, PlanDays = days, PlanValidFrom = validFrom };

    private static IReadOnlyDictionary<int, Medicine> Meds(params Medicine[] medicines)
        => medicines.ToDictionary(m => m.MedicineId);

    private CowTreatment Dose(DateTime date, string cow = CowA, int medicineId = 1)
        => new(++_id, cow, medicineId, date, 1f, 1, 1);

    private static TreatmentPlanCancellation Cancel(CowTreatment start, DateTime on)
        => new() { CowId = start.EarTagNumber, MedicineId = start.MedicineId, StartTreatmentClientId = start.ClientId, CancelledOn = on };

    private static IReadOnlyList<TreatmentPlanRun> Build(
        IReadOnlyDictionary<int, Medicine> meds, IEnumerable<CowTreatment> doses, params TreatmentPlanCancellation[] cancellations)
        => TreatmentPlanRuns.Build(doses, meds, cancellations, Today);

    [Fact]
    public void Medicine_without_plan_produces_no_runs()
    {
        var meds = Meds(new Medicine(1, "Ohne Plan"));
        Assert.Empty(Build(meds, new[] { Dose(Today) }));
    }

    [Fact]
    public void Interval_first_dose_is_upcoming_with_next_due_after_days()
    {
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { Dose(Today.AddDays(-2)) }));
        Assert.Equal(TreatmentPlanRunStatus.Upcoming, run.Status);
        Assert.Equal(Today.AddDays(3), run.NextDueDate);
        Assert.Equal(1, run.DosesDone);
        Assert.Equal(3, run.DoseCount);
        Assert.True(run.IsActive);
        Assert.False(run.IsDue);
    }

    [Fact]
    public void Interval_is_due_today_when_last_dose_plus_days_is_today()
    {
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { Dose(Today.AddDays(-5).AddHours(18)) }));
        Assert.Equal(TreatmentPlanRunStatus.DueToday, run.Status);
        Assert.True(run.IsDue);
    }

    [Fact]
    public void Interval_is_overdue_after_due_date()
    {
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { Dose(Today.AddDays(-7)) }));
        Assert.Equal(TreatmentPlanRunStatus.Overdue, run.Status);
        Assert.Equal(2, run.OverdueDays(Today));
    }

    [Fact]
    public void Interval_late_dose_shifts_next_due_date()
    {
        // Zweite Gabe zwei Tage zu spaet: die dritte ist ab DIESER Gabe faellig.
        var doses = new[] { Dose(Today.AddDays(-12)), Dose(Today.AddDays(-5)) };
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), doses));
        Assert.Equal(Today, run.NextDueDate);
        Assert.Equal(TreatmentPlanRunStatus.DueToday, run.Status);
        Assert.Equal(2, run.DosesDone);
    }

    [Fact]
    public void Run_completes_at_dose_count_and_next_dose_starts_new_run()
    {
        var doses = new[] { Dose(Today.AddDays(-20)), Dose(Today.AddDays(-15)), Dose(Today.AddDays(-1)) };
        var runs = Build(Meds(Med(1, 2, TreatmentPlanMode.Interval, 5)), doses);

        Assert.Equal(2, runs.Count);
        var completed = runs.Single(r => r.Status == TreatmentPlanRunStatus.Completed);
        Assert.Equal(2, completed.DosesDone);
        Assert.Null(completed.NextDueDate);
        var next = runs.Single(r => r.IsActive);
        Assert.Equal(Today.AddDays(-1), next.StartDate);
        Assert.Equal(1, next.DosesDone);
    }

    [Fact]
    public void Window_counts_doses_inside_window_and_reports_window_end()
    {
        var doses = new[] { Dose(Today.AddDays(-2)), Dose(Today.AddDays(-1)) };
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Window, 5)), doses));
        // Starttag = Tag 1 -> letzter Tag = Start + 4.
        Assert.Equal(Today.AddDays(2), run.WindowEnd);
        Assert.Equal(TreatmentPlanRunStatus.Upcoming, run.Status);
        Assert.Null(run.NextDueDate);
        Assert.Equal(2, run.DosesDone);
    }

    [Fact]
    public void Window_is_due_today_when_remaining_doses_fill_remaining_days()
    {
        // 3 in 3 Tagen, eine Gabe gestern: 2 Gaben offen, 2 Tage (heute, morgen) uebrig.
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Window, 3)), new[] { Dose(Today.AddDays(-1)) }));
        Assert.Equal(TreatmentPlanRunStatus.DueToday, run.Status);
    }

    [Fact]
    public void Window_is_not_due_when_already_treated_today()
    {
        var doses = new[] { Dose(Today.AddDays(-1)), Dose(Today.AddHours(7)) };
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Window, 3)), doses));
        Assert.Equal(TreatmentPlanRunStatus.Upcoming, run.Status);
    }

    [Fact]
    public void Window_expired_without_all_doses_is_incomplete()
    {
        var doses = new[] { Dose(Today.AddDays(-10)), Dose(Today.AddDays(-9)) };
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Window, 5)), doses));
        Assert.Equal(TreatmentPlanRunStatus.Incomplete, run.Status);
        Assert.False(run.IsActive);
    }

    [Fact]
    public void Dose_after_window_end_starts_new_run()
    {
        var doses = new[] { Dose(Today.AddDays(-10)), Dose(Today.AddDays(-2)) };
        var runs = Build(Meds(Med(1, 3, TreatmentPlanMode.Window, 5)), doses);

        Assert.Equal(2, runs.Count);
        Assert.Equal(TreatmentPlanRunStatus.Incomplete, runs.Single(r => r.StartDate == Today.AddDays(-10)).Status);
        Assert.True(runs.Single(r => r.StartDate == Today.AddDays(-2)).IsActive);
    }

    [Fact]
    public void Two_doses_on_same_day_count_separately()
    {
        var doses = new[] { Dose(Today.AddHours(8)), Dose(Today.AddHours(18)) };
        var run = Assert.Single(Build(Meds(Med(1, 2, TreatmentPlanMode.Window, 3)), doses));
        Assert.Equal(TreatmentPlanRunStatus.Completed, run.Status);
    }

    [Fact]
    public void Doses_are_ordered_by_date_regardless_of_input_order()
    {
        var late = Dose(Today.AddDays(-1));
        var early = Dose(Today.AddDays(-6));
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { late, early }));
        Assert.Equal(new[] { early, late }, run.Doses);
    }

    [Fact]
    public void Cancelled_run_is_inactive_and_later_dose_starts_new_run()
    {
        var start = Dose(Today.AddDays(-10));
        var later = Dose(Today.AddDays(-3));
        var runs = Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { start, later }, Cancel(start, Today.AddDays(-8)));

        Assert.Equal(2, runs.Count);
        var cancelled = runs.Single(r => r.StartTreatmentClientId == start.ClientId);
        Assert.Equal(TreatmentPlanRunStatus.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.Cancellation);
        Assert.Equal(1, cancelled.DosesDone);
        Assert.True(runs.Single(r => r.StartTreatmentClientId == later.ClientId).IsActive);
    }

    [Fact]
    public void Dose_on_cancellation_day_still_counts_to_cancelled_run()
    {
        var start = Dose(Today.AddDays(-10));
        var sameDay = Dose(Today.AddDays(-8));
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { start, sameDay }, Cancel(start, Today.AddDays(-8))));
        Assert.Equal(TreatmentPlanRunStatus.Cancelled, run.Status);
        Assert.Equal(2, run.DosesDone);
    }

    [Fact]
    public void Completed_wins_over_cancellation()
    {
        var a = Dose(Today.AddDays(-10));
        var b = Dose(Today.AddDays(-5));
        var run = Assert.Single(Build(Meds(Med(1, 2, TreatmentPlanMode.Interval, 5)), new[] { a, b }, Cancel(a, Today)));
        Assert.Equal(TreatmentPlanRunStatus.Completed, run.Status);
    }

    [Fact]
    public void Doses_before_valid_from_are_ignored()
    {
        var meds = Meds(Med(1, 3, TreatmentPlanMode.Interval, 5, validFrom: Today.AddDays(-3)));
        var run = Assert.Single(Build(meds, new[] { Dose(Today.AddDays(-10)), Dose(Today.AddDays(-1)) }));
        Assert.Equal(Today.AddDays(-1), run.StartDate);
    }

    [Fact]
    public void Runs_are_separated_per_cow_and_medicine()
    {
        var meds = Meds(Med(1, 3, TreatmentPlanMode.Interval, 5), Med(2, 2, TreatmentPlanMode.Window, 3));
        var doses = new[]
        {
            Dose(Today.AddDays(-1), CowA, 1), Dose(Today.AddDays(-1), CowB, 1), Dose(Today.AddDays(-1), CowA, 2)
        };
        var runs = Build(meds, doses);

        Assert.Equal(3, runs.Count);
        Assert.All(runs, r => Assert.Equal(1, r.DosesDone));
        Assert.Equal(2, TreatmentPlanRuns.ForCow(runs, CowA).Count);
    }

    [Fact]
    public void ForCow_lists_active_runs_first_then_newest()
    {
        var meds = Meds(Med(1, 2, TreatmentPlanMode.Interval, 5));
        var doses = new[] { Dose(Today.AddDays(-30)), Dose(Today.AddDays(-25)), Dose(Today.AddDays(-20)), Dose(Today.AddDays(-15)), Dose(Today.AddDays(-1)) };
        var runs = TreatmentPlanRuns.ForCow(Build(meds, doses), CowA);

        Assert.Equal(3, runs.Count);
        Assert.True(runs[0].IsActive);
        Assert.Equal(Today.AddDays(-20), runs[1].StartDate);
        Assert.Equal(Today.AddDays(-30), runs[2].StartDate);
    }

    [Fact]
    public void ActiveForDashboard_orders_by_urgency_and_skips_gone_or_unknown_cows()
    {
        var meds = Meds(Med(1, 3, TreatmentPlanMode.Interval, 5));
        var doses = new[]
        {
            Dose(Today.AddDays(-2), "UP"),      // Upcoming
            Dose(Today.AddDays(-7), "OVER"),    // Overdue
            Dose(Today.AddDays(-5), "DUE"),     // DueToday
            Dose(Today.AddDays(-7), "GONE"),    // abgegangen
            Dose(Today.AddDays(-7), "UNKNOWN")  // nicht in der Kuhliste
        };
        var cows = new Dictionary<string, Cow>
        {
            ["UP"] = new("UP", null, 1, false, false),
            ["OVER"] = new("OVER", null, 2, false, false),
            ["DUE"] = new("DUE", null, 3, false, false),
            ["GONE"] = new("GONE", null, 4, false, true)
        };

        var runs = Build(meds, doses);
        var active = TreatmentPlanRuns.ActiveForDashboard(runs, cows);
        Assert.Equal(new[] { "OVER", "DUE", "UP" }, active.Select(r => r.CowId));
        Assert.Equal(new[] { "OVER", "DUE" }, TreatmentPlanRuns.Due(runs, cows).Select(r => r.CowId));
    }
}
