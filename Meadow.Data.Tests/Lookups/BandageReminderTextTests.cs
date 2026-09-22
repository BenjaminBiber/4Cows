using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Der Wortlaut der Verband-Erinnerung.
///
/// Es gibt genau EINEN Text fuer beide Kanaele - die Push-Nachricht des Servers
/// (WebPushSender) und den In-App-Hinweis des Clients
/// (BandageReminderNoticeProvider). Vorher stand er zweimal im Code und war nur
/// per Kommentar als "wortgleich" zugesichert; hier wird die Zusage geprueft
/// statt behauptet. Dasselbe Anti-Drift-Prinzip wie bei der ueberfaellig-Regel.
/// </summary>
public class BandageReminderTextTests
{
    // Festes "heute", damit die Tagesangabe im Text behauptbar ist.
    private static readonly DateTime Today = new(2026, 6, 15);

    private static ClawTreatment Claw(DateTime date, string cowId = "DE0815")
        => new(0, cowId, date,
            null, true, false,
            null, false, false,
            null, false, false,
            null, false, false,
            false);

    private static IReadOnlyDictionary<string, Cow> Herd(params Cow[] cows)
        => cows.ToDictionary(c => c.CowId);

    [Fact]
    public void The_text_names_ear_tag_and_collar_number()
    {
        // Der Regelfall: erwachsene Kuh, deren Cow_ID zugleich die Ohrmarke ist.
        // Der ganze Satz wird behauptet, nicht nur ein Teilstueck - er ist die
        // Zusage, die beide Kanaele einloesen.
        var herd = Herd(new Cow("DE0815", "DE0815", 127, false, false));

        var text = BandageReminderText.For(Claw(Today.AddDays(-19)), herd, Today);

        Assert.Equal("Verband seit 19 Tagen: DE0815 (Halsband 127)", text);
    }

    [Fact]
    public void Without_a_matching_cow_the_text_stays_readable()
    {
        // Die Kuh ist nicht (mehr) im Cache. Der Hinweis darf deswegen nicht
        // ausfallen und keinen leeren Klammerausdruck zeigen - er nennt dann
        // eben nur die Ohrmarke.
        var text = BandageReminderText.For(Claw(Today.AddDays(-19)), Herd(), Today);

        Assert.Equal("Verband seit 19 Tagen: DE0815", text);
    }

    [Fact]
    public void A_cow_without_a_collar_number_gets_no_collar_part()
    {
        // CollarNumber ist ein int ohne Null-Zustand: "kein Halsband" kommt als
        // 0 an. "(Halsband 0)" waere eine Falschaussage im Stall.
        var herd = Herd(new Cow("DE0815", "DE0815", 0, false, false));

        var text = BandageReminderText.For(Claw(Today.AddDays(-3)), herd, Today);

        Assert.DoesNotContain("Halsband", text);
    }

    [Fact]
    public void One_day_overdue_is_singular()
    {
        var herd = Herd(new Cow("DE0815", "DE0815", 127, false, false));

        var text = BandageReminderText.For(Claw(Today.AddDays(-1)), herd, Today);

        Assert.Equal("Verband seit 1 Tag: DE0815 (Halsband 127)", text);
    }

    [Fact]
    public void The_time_of_day_does_not_change_the_number_of_days()
    {
        // Verglichen wird auf Tagesebene - wie in der ueberfaellig-Regel. Eine
        // Uhrzeit darf aus "seit 2 Tagen" nicht "seit 1 Tag" machen.
        var herd = Herd(new Cow("DE0815", "DE0815", 127, false, false));
        var treatment = Claw(Today.AddDays(-2).AddHours(23));

        var text = BandageReminderText.For(treatment, herd, Today.AddHours(1));

        Assert.Contains("seit 2 Tagen", text);
    }
}
