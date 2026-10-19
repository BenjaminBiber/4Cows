using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Wortlaut der Behandlungsplan-Erinnerungen. Eine Textstelle fuer Hinweis
/// (Client) und Push (Server) - wie beim Verband.
/// </summary>
public class TreatmentPlanReminderTextTests
{
    private static readonly DateTime Today = new(2026, 6, 15);

    private static readonly IReadOnlyDictionary<string, Cow> Cows = new Dictionary<string, Cow>
    {
        ["DE01"] = new("DE01", "DE01", 12, false, false),
        ["DE02"] = new("DE02", "DE02", 0, false, false)
    };

    private static TreatmentPlanRun Run(string cowId, TreatmentPlanRunStatus status, int done = 1, DateTime? nextDue = null)
        => new()
        {
            CowId = cowId,
            MedicineId = 1,
            StartTreatmentClientId = Guid.NewGuid(),
            Mode = TreatmentPlanMode.Interval,
            DoseCount = 3,
            PlanDays = 5,
            Doses = Enumerable.Range(0, done).Select(i => new CowTreatment(i + 1, cowId, 1, Today.AddDays(-10 + i), 1f, 1, 1)).ToList(),
            Status = status,
            NextDueDate = nextDue
        };

    [Fact]
    public void Due_today_names_next_dose_medicine_and_cow_with_collar()
        => Assert.Equal("Gabe 2/3 Synulox heute fällig: DE01 (Halsband 12)",
            TreatmentPlanReminderText.For(Run("DE01", TreatmentPlanRunStatus.DueToday, nextDue: Today), "Synulox", Cows, Today));

    [Fact]
    public void Overdue_counts_days_singular_and_plural()
    {
        Assert.Equal("Gabe 2/3 Synulox seit 1 Tag überfällig: DE02",
            TreatmentPlanReminderText.For(Run("DE02", TreatmentPlanRunStatus.Overdue, nextDue: Today.AddDays(-1)), "Synulox", Cows, Today));
        Assert.Equal("Gabe 3/3 Synulox seit 4 Tagen überfällig: DE02",
            TreatmentPlanReminderText.For(Run("DE02", TreatmentPlanRunStatus.Overdue, done: 2, nextDue: Today.AddDays(-4)), "Synulox", Cows, Today));
    }

    [Fact]
    public void Push_summary_is_null_when_nothing_is_due()
        => Assert.Null(TreatmentPlanReminderText.PushSummary(Array.Empty<TreatmentPlanRun>()));

    [Fact]
    public void Push_summary_counts_cows_due_and_overdue()
    {
        var runs = new[]
        {
            Run("DE01", TreatmentPlanRunStatus.DueToday, nextDue: Today),
            Run("DE02", TreatmentPlanRunStatus.DueToday, nextDue: Today),
            Run("DE03", TreatmentPlanRunStatus.Overdue, nextDue: Today.AddDays(-2))
        };
        Assert.Equal("2 Kühe heute fällig, 1 Kuh überfällig", TreatmentPlanReminderText.PushSummary(runs));
    }

    [Fact]
    public void Push_summary_counts_a_cow_once_even_with_two_due_plans()
    {
        var runs = new[]
        {
            Run("DE01", TreatmentPlanRunStatus.DueToday, nextDue: Today),
            Run("DE01", TreatmentPlanRunStatus.DueToday, nextDue: Today) with { MedicineId = 2 }
        };
        Assert.Equal("1 Kuh heute fällig", TreatmentPlanReminderText.PushSummary(runs));
    }

    [Fact]
    public void Push_summary_only_overdue()
        => Assert.Equal("1 Kuh überfällig",
            TreatmentPlanReminderText.PushSummary(new[] { Run("DE01", TreatmentPlanRunStatus.Overdue, nextDue: Today.AddDays(-1)) }));
}
