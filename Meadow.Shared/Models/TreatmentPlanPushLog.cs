using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Meadow.Shared.Models;

/// <summary>
/// Merker "Sammel-Push Behandlungsplaene an diesem Tag verschickt". Eigene
/// Tabelle wie <see cref="PushReminderLog"/>: ein Senden darf keine
/// Datenversion bumpen und gehoert nicht an die fachlichen Bewegungsdaten.
///
/// Anders als beim Verband gibt es hier keinen Schluessel je Behandlung - die
/// Push ist EINE Sammelmeldung fuer alle faelligen Plaene, also reicht der Tag
/// als Schluessel. Unique auf <see cref="SentOn"/>: ein zweiter Lauf am selben
/// Tag scheitert am Index, falls zwei Instanzen gleichzeitig senden wollten.
///
/// Persistent und nicht In-Memory aus demselben Grund wie beim Verband: die
/// Abnahme verlangt, dass auch nach einem Neustart am selben Tag nichts erneut
/// hinausgeht.
/// </summary>
[Table("Treatment_Plan_Push_Log")]
public class TreatmentPlanPushLog
{
    [Key]
    [Column("Treatment_Plan_Push_Log_ID")]
    public int TreatmentPlanPushLogId { get; set; }

    /// <summary>
    /// Der Tag, an dem die Sammel-Push hinausging. Gespeichert wird stets
    /// <c>DateTime.Today</c>, verglichen wird nur auf Tagesebene (siehe
    /// <c>PushLogic.ShouldSendReminderToday</c>).
    /// </summary>
    [Required]
    [Column("Sent_On")]
    public DateTime SentOn { get; set; }
}
