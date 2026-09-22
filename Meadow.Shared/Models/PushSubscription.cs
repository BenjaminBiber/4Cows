using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Meadow.Shared.Models;

/// <summary>
/// Eine Web-Push-Anmeldung eines Browsers/Geraets.
///
/// Das ist KEIN Client-Cache der dreizehn Dienste, sondern ein serverseitiger
/// Kanal: der Browser meldet sich einmal beim Push-Dienst seines Herstellers an
/// (Google/Mozilla/Apple) und uebergibt uns das Ergebnis - eine Endpunkt-URL
/// samt zweier Schluessel. Damit kann der Server SPAETER, auch bei geschlossener
/// App, eine verschluesselte Nachricht an genau diesen Browser schicken. Task 3
/// legt nur den Kanal an; das fachliche Senden (Verband-Erinnerung) kommt in
/// Task 6.
///
/// Der <see cref="Endpoint"/> ist der eindeutige Schluessel: ein Browser hat je
/// Anmeldung genau einen, und ein erneutes Subscriben liefert denselben zurueck.
/// Deshalb ist wiederholtes Anmelden ein Upsert und keine zweite Zeile - der
/// Unique-Index in <c>DatabaseContext.OnModelCreating</c> ist die verlaessliche
/// Garantie dafuer, die Pruefung im Endpunkt nur der schnelle Weg dorthin.
/// </summary>
[Table("PushSubscription")]
public class PushSubscription
{
    [Key]
    [Column("PushSubscription_ID")]
    public int PushSubscriptionId { get; set; }

    /// <summary>
    /// Die vom Browser-Push-Dienst vergebene Ziel-URL. Eindeutig je Anmeldung
    /// und teils sehr lang (FCM-Endpunkte laufen ueber 200 Zeichen), deshalb
    /// varchar(512). Der Unique-Index sitzt hier - er macht aus wiederholtem
    /// Subscriben einen No-op.
    /// </summary>
    [Required]
    [StringLength(512)]
    [Column("Endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Der oeffentliche P-256-Schluessel des Browsers (base64url). Der Server
    /// verschluesselt damit die Nutzlast, sodass nur dieser Browser sie lesen
    /// kann - der Push-Dienst dazwischen sieht nur Chiffrat.
    /// </summary>
    [Required]
    [StringLength(256)]
    [Column("P256dh")]
    public string P256dh { get; set; } = string.Empty;

    /// <summary>
    /// Das Auth-Geheimnis des Browsers (base64url), das zweite Stueck des
    /// Web-Push-Verschluesselungsverfahrens (RFC 8291).
    /// </summary>
    [Required]
    [StringLength(256)]
    [Column("Auth")]
    public string Auth { get; set; } = string.Empty;

    /// <summary>
    /// Der User-Agent zum Zeitpunkt der Anmeldung - rein informativ, damit man
    /// eine tote Anmeldung im Zweifel einem Geraet zuordnen kann. Optional.
    /// </summary>
    [StringLength(256)]
    [Column("UserAgent")]
    public string? UserAgent { get; set; }

    /// <summary>Wann die Anmeldung entstand. Rein informativ.</summary>
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
