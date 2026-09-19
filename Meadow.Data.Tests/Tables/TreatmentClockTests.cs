using Meadow.Client.Components.Ui;

namespace Meadow.Data.Tests.Tables;

/// <summary>
/// Der Zeitstempel einer erfassten Behandlung. Er entscheidet, ob mehrere
/// Behandlungen desselben Tages in der Tabelle auseinandergehalten werden
/// koennen: die Sortierung laeuft ueber den vollen DateTime, also wirkt die
/// Uhrzeit als Feinsortierung innerhalb des Tages.
/// </summary>
public class TreatmentClockTests
{
    [Fact]
    public void A_treatment_recorded_today_keeps_the_current_time()
    {
        // Heute erfasst - die Uhrzeit gehoert dazu, sonst stuenden zwei
        // Behandlungen desselben Tages als gleichwertige 00:00-Eintraege
        // nebeneinander.
        var now = new DateTime(2026, 9, 19, 14, 37, 12);
        var chosen = now.Date; // der Datums-Picker liefert Mitternacht

        var stamp = TreatmentClock.StampFor(chosen, now);

        Assert.Equal(new DateTime(2026, 9, 19, 14, 37, 12), stamp);
    }

    [Fact]
    public void A_back_dated_treatment_stays_at_midnight()
    {
        // Rueckdatiert - die aktuelle Uhrzeit waere hier schlicht falsch, also
        // bleibt der Eintrag auf Mitternacht.
        var now = new DateTime(2026, 9, 19, 14, 37, 12);
        var chosen = new DateTime(2026, 9, 10);

        var stamp = TreatmentClock.StampFor(chosen, now);

        Assert.Equal(new DateTime(2026, 9, 10, 0, 0, 0), stamp);
    }

    [Fact]
    public void A_time_already_on_the_chosen_value_does_not_leak_into_a_past_day()
    {
        // Selbst wenn das gewaehlte Datum aus irgendeinem Grund schon eine
        // Uhrzeit traegt (der Kuh-Dialog startet mit DateTime.Now), zaehlt fuer
        // einen vergangenen Tag nur der Kalendertag.
        var now = new DateTime(2026, 9, 19, 14, 37, 12);
        var chosen = new DateTime(2026, 9, 10, 8, 15, 0);

        var stamp = TreatmentClock.StampFor(chosen, now);

        Assert.Equal(new DateTime(2026, 9, 10, 0, 0, 0), stamp);
    }
}
