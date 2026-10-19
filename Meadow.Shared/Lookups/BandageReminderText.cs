using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Der Wortlaut der Verband-Erinnerung - EINMAL, fuer beide Kanaele.
///
/// Die Push-Nachricht (WebPushSender, Server) und der In-App-Hinweis
/// (BandageReminderNoticeProvider, Client) sagen bewusst dasselbe: der Nutzer
/// soll zur selben Sache nicht zwei Formulierungen sehen. Vorher stand der Satz
/// dafuer zweimal im Code und war nur per Kommentar als "wortgleich"
/// zugesichert - eine Zusage, die beim naechsten Umformulieren still bricht.
/// Hier steht er an einer Stelle und ist geprueft; dasselbe Anti-Drift-Prinzip
/// wie bei <see cref="ClawTreatmentExtensions.OverdueBandages"/>.
///
/// Rein und ohne Uhr: <paramref name="today"/> ist Parameter, damit die
/// Tagesangabe behauptbar bleibt und auf einem Client mit anderer Zeitzone
/// dieselbe ist.
/// </summary>
public static class BandageReminderText
{
    /// <summary>
    /// Der Erinnerungstext zu einer ueberfaelligen Behandlung. Die
    /// Faelligkeit selbst entscheidet die gemeinsame Regel
    /// (<see cref="ClawTreatmentExtensions.IsBandageOverdue"/>) - hier wird nur
    /// noch formuliert.
    /// </summary>
    public static string For(ClawTreatment treatment, IReadOnlyDictionary<string, Cow> cows, DateTime today)
    {
        // Ueberfaellig SEIT: Tage ueber den Behandlungstag hinaus, auf
        // Tagesebene wie die ueberfaellig-Regel. Eine Uhrzeit darf die Zahl
        // nicht verschieben.
        var overdueDays = (today.Date - treatment.TreatmentDate.Date).Days;
        var seit = overdueDays == 1 ? "seit 1 Tag" : $"seit {overdueDays} Tagen";

        // Dringlichkeit zuerst, dann das Tier: beim Ueberfliegen der Kachel
        // zaehlt, wie lange der Verband schon liegt. "bitte abnehmen" ist
        // weggefallen - die Kachel fuehrt ohnehin auf die Verbaende-Seite, und
        // die Push-Nachricht traegt "Verband-Erinnerung" als Titel.
        return $"Verband {seit}: {treatment.EarTagNumber}"
               // Die Halsbandnummer in Klammern - das Merkmal, an dem das Tier
               // im Stall tatsaechlich gefunden wird. Nachgeschlagen wird ueber
               // die Cow_ID: ClawTreatment.EarTagNumber traegt trotz seines
               // Namens die Cow_ID. Derselbe Zusatz wie bei der
               // Behandlungsplan-Erinnerung, deshalb in CowLookups.
               + CowLookups.CollarSuffix(cows, treatment.EarTagNumber);
    }
}
