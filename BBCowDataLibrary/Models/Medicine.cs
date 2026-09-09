using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class;

[Table("Medicine")]
public class Medicine
{
    /// <summary>
    /// Laengengrenze von <see cref="MedicineName"/>, von 64 auf 190 verbreitert.
    ///
    /// Handelsnamen tragen haeufig Staerke und Darreichungsform mit:
    /// "Ubrolexin 100 mg/ml Injektionssuspension fuer Rinder" ist schon 52
    /// Zeichen. Bei 64 waeren solche Namen genau dort abgeschnitten, wo sie
    /// sich unterscheiden, und die Liste haette optisch identische Zeilen.
    ///
    /// 190 und nicht 256: das ist die unter aelteren InnoDB-Zeilenformaten
    /// indexsichere Breite fuer utf8mb4 (767/4). Auf Medicine_Name liegt heute
    /// kein Index - geprueft, InitialCreate legt nur den Primaerschluessel an -
    /// aber ein spaeter hinzugefuegter waere damit moeglich, ohne die Spalte
    /// erneut anfassen zu muessen.
    /// </summary>
    public const int NameMaxLength = 190;

    [Key]
    [Column("Medicine_ID")]
    public int MedicineId { get; set; }

    [Required]
    [StringLength(NameMaxLength)]
    [Column("Medicine_Name")]
    public string MedicineName { get; set; }

    /// <summary>
    /// Einheit der Menge ("ml", "Stueck", "g"). <c>null</c> heisst "unbekannt",
    /// nicht "ml": die Tabellen fallen dann auf ihren bisherigen Anzeigewert
    /// zurueck, statt eine Tablette in Millilitern auszuweisen.
    /// </summary>
    [StringLength(16)]
    [Column("Dosage_Unit")]
    public string? DosageUnit { get; set; }

    /// <summary>
    /// Vorbelegung fuer "Wie / Wo" in den Behandlungsdialogen. Bewusst ohne
    /// Fremdschluessel - das Schema aus 20251223183944_InitialCreate legt
    /// nirgends welche an, aufgeloest wird ueber den WhereHowService-Cache.
    /// </summary>
    [Column("Default_WhereHow_ID")]
    public int? DefaultWhereHowId { get; set; }

    public Medicine() : this(0, string.Empty) { }

    public Medicine(int medicineId, string medicineName)
    {
        MedicineId = medicineId;
        MedicineName = medicineName;
    }
}
