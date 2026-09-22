using Meadow.Shared.Lookups;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Der Kern der Task-6-Abnahme als reine Regel:
/// <see cref="PushLogic.ShouldSendReminderToday"/> - "soll fuer diese
/// Behandlung HEUTE (noch) eine Verband-Erinnerung raus, gegeben zuletzt
/// gesendet am?".
///
/// Ohne Datenbank, Netz und Uhr pruefbar; das echte Push-Versenden bleibt
/// laufzeit-only. "Heute" ist Parameter statt DateTime.Now, damit die
/// Tagesgrenze behauptbar ist - dieselbe Haltung wie die ueberfaellig-Regel in
/// BandageOverdueRuleTests. Persistent gemacht wird der Merker in der
/// PushReminderLog-Tabelle; diese Regel entscheidet nur ueber den gelesenen
/// Wert und haelt damit auch ueber einen Neustart hinweg.
/// </summary>
public class PushReminderDedupTests
{
    // Festes "heute" fuer jeden Test, damit die Tagesgrenze behauptbar ist.
    private static readonly DateTime Today = new(2026, 6, 15, 8, 0, 0);

    [Fact]
    public void First_run_ever_sends_because_nothing_was_sent_yet()
    {
        // Noch kein Merker (null) -> erster Lauf schickt.
        Assert.True(PushLogic.ShouldSendReminderToday(lastSentOn: null, Today));
    }

    [Fact]
    public void Second_run_the_same_day_does_not_send_again()
    {
        // Der Abnahme-Kern: heute frueh schon geschickt -> ein zweiter Lauf am
        // selben Tag schickt NICHTS erneut. Weil der Merker persistent ist, gilt
        // das auch nach einem Neustart zwischen den beiden Laeufen.
        var sentEarlierToday = new DateTime(2026, 6, 15, 6, 30, 0);

        Assert.False(PushLogic.ShouldSendReminderToday(sentEarlierToday, Today));
    }

    [Fact]
    public void The_next_day_sends_again()
    {
        // Zuletzt gestern -> am naechsten Tag wieder faellig.
        var sentYesterday = new DateTime(2026, 6, 14, 22, 0, 0);

        Assert.True(PushLogic.ShouldSendReminderToday(sentYesterday, Today));
    }

    [Fact]
    public void A_later_time_of_day_does_not_reopen_the_send_because_comparison_is_by_day()
    {
        // Merker heute morgen, Entscheidung heute spaet abends: die Uhrzeit darf
        // nicht dazu fuehren, dass am selben Tag doch nochmal gesendet wird -
        // verglichen wird auf Tagesebene.
        var sentThisMorning = new DateTime(2026, 6, 15, 7, 0, 0);
        var laterToday = new DateTime(2026, 6, 15, 23, 59, 0);

        Assert.False(PushLogic.ShouldSendReminderToday(sentThisMorning, laterToday));
    }

    [Fact]
    public void A_much_older_send_date_sends_again()
    {
        // Zuletzt vor Wochen -> laengst wieder faellig.
        var sentLongAgo = new DateTime(2026, 5, 1);

        Assert.True(PushLogic.ShouldSendReminderToday(sentLongAgo, Today));
    }
}
