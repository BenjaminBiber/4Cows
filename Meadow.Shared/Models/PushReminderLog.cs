using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Meadow.Shared.Models;

/// <summary>
/// Der persistente Merker der Verband-Push-Entdopplung: je Behandlung die
/// zuletzt versendete Erinnerung, auf Tagesebene.
///
/// Warum eine eigene Tabelle statt einer Spalte an ClawTreatment: ClawTreatment
/// ist eine der vier offline schreibbaren Tabellen mit ClientId-Idempotenz und
/// liegt im WebAssembly-Client. Eine reine Server-Sendehistorie hat dort nichts
/// verloren - sie waere ein Feld, das der Client nie fuellt und dessen Bump
/// jeden Tablet-Cache grundlos veralten liesse. Die getrennte Tabelle haelt den
/// server-initiierten Kanal (wie PushSubscription) sauber von den fachlichen
/// Bewegungsdaten getrennt.
///
/// Warum persistent und nicht In-Memory: die Abnahme verlangt, dass ein
/// ZWEITER Lauf am selben Tag - auch nach einem Neustart - nichts erneut
/// sendet. Ein In-Memory-Set waere nach dem Neustart leer und schickte alles
/// noch einmal.
///
/// <see cref="ClawTreatmentId"/> ist der eindeutige Schluessel: je Behandlung
/// genau eine Zeile, die bei jedem Versand auf das heutige Datum gehoben wird.
/// Verglichen wird ausschliesslich auf Tagesebene - dieselbe Tagesebene wie die
/// ueberfaellig-Regel in <c>ClawTreatmentExtensions.IsBandageOverdue</c>.
/// </summary>
[Table("PushReminderLog")]
public class PushReminderLog
{
    /// <summary>
    /// Die Behandlung, fuer deren Verband zuletzt erinnert wurde. Zugleich der
    /// Primaerschluessel - je Behandlung genau eine Merker-Zeile, kein
    /// separates Identity-Feld noetig.
    /// </summary>
    [Key]
    [Column("Claw_Treatment_ID")]
    public int ClawTreatmentId { get; set; }

    /// <summary>
    /// Der Tag, an dem zuletzt eine Push-Erinnerung fuer diese Behandlung
    /// hinausging. Nur die Tageskomponente ist bedeutsam; die Zeit wird bei der
    /// Entscheidung nie herangezogen (siehe <c>PushLogic.ShouldSendReminderToday</c>).
    /// </summary>
    [Column("SentOn")]
    public DateTime SentOn { get; set; }
}
