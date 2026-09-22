using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Models;

/// <summary>
/// Die gemeinsame ueberfaellig-Regel fuer Verbaende
/// (<see cref="ClawTreatmentExtensions.IsBandageOverdue"/> und
/// <see cref="ClawTreatmentExtensions.OverdueBandages"/>).
///
/// Eine Regel, zwei Verbraucher (In-App-Hinweis + Push-Scheduler) - deshalb wird
/// sie hier einmal abgesichert, nicht in jedem Verbraucher. "Heute" ist
/// Parameter statt Uhr im Rumpf, damit die Grenzen behauptbar sind und auf einem
/// Client mit anderer Zeitzone dieselben bleiben - wie bei der Jahresachse.
/// </summary>
public class BandageOverdueRuleTests
{
    // Festes "heute" fuer jeden Test, damit die Grenzen behauptbar sind.
    private static readonly DateTime Today = new(2026, 6, 15);
    private const int ReminderDays = 14;

    private static ClawTreatment Claw(
        DateTime date,
        bool lv = false,
        bool lh = false,
        bool rv = false,
        bool rh = false,
        bool removed = false)
        => new(0, "DE0815", date,
            null, lv, false,
            null, lh, false,
            null, rv, false,
            null, rh, false,
            removed);

    [Fact]
    public void An_open_bandage_older_than_the_reminder_days_is_overdue()
    {
        // Behandlung 20 Tage her, offener Verband -> 14-Tage-Grenze klar ueberschritten.
        var t = Claw(Today.AddDays(-20), lv: true);

        Assert.True(t.IsBandageOverdue(ReminderDays, Today));
    }

    [Fact]
    public void One_open_overdue_treatment_yields_exactly_one_hit()
    {
        var overdue = Claw(Today.AddDays(-20), lv: true);
        var freshEnough = Claw(Today.AddDays(-2), rh: true);
        var noBandage = Claw(Today.AddDays(-30));

        var result = ClawTreatmentExtensions
            .OverdueBandages(new[] { overdue, freshEnough, noBandage }, ReminderDays, Today)
            .ToList();

        Assert.Single(result);
        Assert.Same(overdue, result[0]);
    }

    [Fact]
    public void A_removed_bandage_is_never_overdue_even_when_old()
    {
        var t = Claw(Today.AddDays(-100), lv: true, removed: true);

        Assert.False(t.IsBandageOverdue(ReminderDays, Today));
        Assert.Empty(ClawTreatmentExtensions.OverdueBandages(new[] { t }, ReminderDays, Today));
    }

    [Fact]
    public void A_treatment_without_any_open_bandage_is_never_overdue()
    {
        var t = Claw(Today.AddDays(-100));

        Assert.False(t.IsBandageOverdue(ReminderDays, Today));
        Assert.Empty(ClawTreatmentExtensions.OverdueBandages(new[] { t }, ReminderDays, Today));
    }

    [Fact]
    public void The_day_the_reminder_lands_exactly_on_today_already_counts_as_overdue()
    {
        // Grenzfall: TreatmentDate.AddDays(n).Date == heute -> <= greift, faellig.
        var t = Claw(Today.AddDays(-ReminderDays), lv: true);

        Assert.True(t.IsBandageOverdue(ReminderDays, Today));
    }

    [Fact]
    public void The_day_before_the_reminder_lands_is_not_yet_overdue()
    {
        // Ein Tag vor der Grenze: AddDays(n).Date liegt morgen, noch nicht faellig.
        var t = Claw(Today.AddDays(-(ReminderDays - 1)), lv: true);

        Assert.False(t.IsBandageOverdue(ReminderDays, Today));
    }

    [Fact]
    public void A_time_of_day_does_not_delay_faelligkeit_because_comparison_is_by_day()
    {
        // Behandlung heute frueh vor genau n Tagen, jetzt am spaeten Abend: die
        // Uhrzeit darf nicht entscheiden, verglichen wird auf Tagesebene.
        var treatedMorning = new DateTime(2026, 6, 1, 7, 0, 0);
        var nowEvening = new DateTime(2026, 6, 15, 23, 0, 0);
        var t = Claw(treatedMorning, lv: true); // n = 14 Tage -> 15. Juni

        Assert.True(t.IsBandageOverdue(ReminderDays, nowEvening));
    }
}
