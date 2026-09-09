using BB_Cow.Class;
using BB_Cow.Kpi;

namespace BB_Cow.Profile;

/// <summary>Ein Wert und wie oft er vorkommt. Immer absteigend sortiert.</summary>
public sealed record CowProfileTally(string Key, int Count);

/// <summary>
/// Wie <see cref="CowProfileTally"/>, aber der Schluessel ist eine Lookup-ID.
/// Namen loest die Seite auf - die Bibliothek kennt MedicineService nicht, und
/// GetMedicineNameById liefert bei unbekannter ID das Literal "--", das hier
/// nichts zu suchen haette.
/// </summary>
public sealed record CowProfileIdTally(int Id, int Count);

/// <summary>Ein Monat der rollenden Zwoelfmonatsreihe. Aeltester zuerst.</summary>
public sealed record CowProfileMonth(int Year, int Month, int CowCount, int ClawCount)
{
    public int Total => CowCount + ClawCount;
}

/// <summary>Eine Klauenposition ueber alle Behandlungen dieser Kuh.</summary>
/// <param name="TreatmentCount">Behandlungen mit IRGENDEINER Erfassung an dieser Klaue (HasData).</param>
/// <param name="BandageCount">Je erfasster Verband, unabhaengig davon ob er schon ab ist.</param>
/// <param name="OpenBandageCount">Nur die Verbaende, die noch liegen.</param>
public sealed record CowProfileHoof(
    HoofPosition Position,
    int TreatmentCount,
    int BandageCount,
    int OpenBandageCount,
    int BlockCount,
    IReadOnlyList<CowProfileTally> Findings);

/// <summary>
/// Alles, was die Kuh-Seite ueber ein Tier rechnet - in einem Rutsch und ohne
/// jede Abhaengigkeit auf DI, EF oder die Service-Caches.
///
/// Bewusst ein Wertobjekt und kein Service mit Cache: es gibt in dieser
/// Anwendung kein Aenderungsereignis, an dem man einen Cache invalidieren
/// koennte. KpiRowProvider begruendet das ausfuehrlich; hier gilt dasselbe,
/// und ein Profil haette nicht einmal einen natuerlichen Cache-Schluessel.
/// </summary>
public sealed record CowProfile
{
    /// <summary>Sammelposten "ohne Grund" in <see cref="Reasons"/>.</summary>
    public const int NoReasonId = int.MinValue;

    public required string CowId { get; init; }

    // Bereits auf diese Kuh gefiltert und nach Datum absteigend sortiert - die
    // fuenf Tabellen der Seite binden direkt hierauf, statt noch einmal ueber
    // die Service-Caches zu laufen.
    public required IReadOnlyList<CowTreatment> CowTreatments { get; init; }
    public required IReadOnlyList<ClawTreatment> ClawTreatments { get; init; }
    public required IReadOnlyList<PlannedCowTreatment> PlannedCowTreatments { get; init; }
    public required IReadOnlyList<PlannedClawTreatment> PlannedClawTreatments { get; init; }

    public int TotalCowTreatments => CowTreatments.Count;
    public int TotalClawTreatments => ClawTreatments.Count;
    public int TotalTreatments => TotalCowTreatments + TotalClawTreatments;

    /// <summary>Juengste erfasste Behandlung beider Arten, null wenn es keine gibt.</summary>
    public required DateTime? LastTreatment { get; init; }

    public required int Last12Months { get; init; }
    public required int Previous12Months { get; init; }

    /// <summary>
    /// Absolut ("+3", "-2", "&#177;0") und NICHT prozentual wie auf dem
    /// Dashboard. Bei einem einzelnen Tier ist der Vorzeitraum oft eine oder
    /// zwei Behandlungen - "+100 %" waere formal richtig und praktisch
    /// Rauschen. Null heisst: kein Vergleich moeglich, dann zeigt die Kachel
    /// die Zeile gar nicht erst.
    /// </summary>
    public required string? DeltaDisplay { get; init; }

    public required KpiTrend Trend { get; init; }

    /// <summary>Genau zwoelf Eintraege, aeltester zuerst.</summary>
    public required IReadOnlyList<CowProfileMonth> Months { get; init; }

    /// <summary>Genau vier Eintraege in der Reihenfolge von HoofPositions.All.</summary>
    public required IReadOnlyList<CowProfileHoof> Hoofs { get; init; }

    public required IReadOnlyList<CowProfileIdTally> Medicines { get; init; }

    /// <summary>Behandlungen ohne Grund sammeln sich unter <see cref="NoReasonId"/>.</summary>
    public required IReadOnlyList<CowProfileIdTally> Reasons { get; init; }

    /// <summary>Klauenbefunde ueber alle vier Positionen zusammen.</summary>
    public required IReadOnlyList<CowProfileTally> Findings { get; init; }

    /// <summary>
    /// Behandlungen je Euterviertel. Eine Behandlung kann mehrere Viertel
    /// treffen - die vier Zahlen summieren sich also hoeher als
    /// <see cref="UdderTreatments"/>, und die Seite muss das dazusagen.
    /// </summary>
    public required IReadOnlyDictionary<HoofPosition, int> UdderQuarters { get; init; }

    /// <summary>Kuhbehandlungen MIT Viertelangabe.</summary>
    public required int UdderTreatments { get; init; }

    /// <summary>
    /// Offene Verbaende als KLAUEN gezaehlt - dieselbe Menge, die die
    /// Verbaende-Tabelle als Zeilen zeigt (siehe ClawTreatment.OpenBandages).
    /// </summary>
    public required int OpenBandages { get; init; }

    /// <summary>
    /// Nicht erledigte geplante Termine. PlannedClawTreatment hat kein
    /// Erledigt-Kennzeichen - dort loescht der Abschluss die Zeile -, deshalb
    /// zaehlen davon alle.
    /// </summary>
    public required int OpenPlanned { get; init; }

    public bool HasAnyTreatment => TotalTreatments > 0;

    /// <summary>Die haeufigste Position, oder null wenn es keine Klauenbehandlung gibt.</summary>
    public CowProfileTally? TopFinding => Findings.Count > 0 ? Findings[0] : null;
}
