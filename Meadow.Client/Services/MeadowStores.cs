using Meadow.Client.Components.Services;

namespace Meadow.Client.Services;

/// <summary>
/// Welche Datenart ein Outbox-Eintrag traegt.
///
/// Absichtlich NICHT <see cref="MeadowDataKind"/> wiederverwendet: der Enum
/// dort sagt einer Seite, ob sie sich angesprochen fuehlen soll, und wird von
/// .razor-Dateien gelesen. Dieser hier ist ein PERSISTIERTER Wert - er liegt
/// als Zahl in IndexedDB und ueberlebt App-Neustarts. Die beiden duerfen sich
/// nicht gegenseitig die Reihenfolge diktieren; <see cref="MeadowStores.KindOf"/>
/// uebersetzt an genau einer Stelle.
/// </summary>
public enum MeadowEntityType
{
    CowTreatment = 0,
    ClawTreatment = 1,
    PlannedCowTreatment = 2,
    PlannedClawTreatment = 3
}

/// <summary>
/// Die Namen der Object Stores und was zu ihnen gehoert - Route, Datenart,
/// Schluesselpfad.
///
/// Einmal hier und nicht je einmal im Dienst, im Handler und im Prozessor:
/// ein Tippfehler im Store-Namen legt keine Ausnahme hin, sondern einen
/// leeren Store. Der faellt erst offline auf.
/// </summary>
public static class MeadowStores
{
    public const string Cow = "cow";
    public const string Medicine = "medicine";
    public const string WhereHow = "whereHow";
    public const string TreatmentReason = "treatmentReason";
    public const string ClawFinding = "clawFinding";
    public const string Udder = "udder";
    public const string AppSetting = "appSetting";
    public const string Kpi = "kpi";

    public const string CowTreatment = "cowTreatment";
    public const string ClawTreatment = "clawTreatment";
    public const string PlannedCowTreatment = "plannedCowTreatment";
    public const string PlannedClawTreatment = "plannedClawTreatment";

    /// <summary>Zuletzt gesehener X-Data-Version-Kopf.</summary>
    public const string MetaDataVersion = "dataVersion";

    /// <summary>Naechste vorlaeufige Id. Zaehlt ABWAERTS, siehe MeadowOutbox.</summary>
    public const string MetaNextProvisionalId = "nextProvisionalId";

    /// <summary>Zeitpunkt der letzten erfolgreichen Uebertragung.</summary>
    public const string MetaLastSyncUtc = "lastSyncUtc";

    /// <summary>
    /// Die zwoelf Tabellen-GETs, deren Antwort gespiegelt wird. Schluessel ist
    /// der Pfad hinter /api/, damit ein Unterverzeichnis im Hosting nichts
    /// verschiebt.
    ///
    /// Bewusst nur EXAKTE Treffer: /api/medicines/usage-counts ist eine
    /// Auswertung und kein Tabellenabzug - lokal gespeichert saehe sie aus wie
    /// die Medikamentenliste.
    /// </summary>
    private static readonly Dictionary<string, string> ByPath = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cows"] = Cow,
        ["medicines"] = Medicine,
        ["where-hows"] = WhereHow,
        ["treatment-reasons"] = TreatmentReason,
        ["claw-findings"] = ClawFinding,
        ["udders"] = Udder,
        ["settings"] = AppSetting,
        ["kpis"] = Kpi,
        ["cow-treatments"] = CowTreatment,
        ["claw-treatments"] = ClawTreatment,
        ["planned-cow-treatments"] = PlannedCowTreatment,
        ["planned-claw-treatments"] = PlannedClawTreatment
    };

    /// <summary>
    /// Der Store zu einem Anfragepfad, oder <c>null</c> fuer alles, was nicht
    /// gespiegelt wird.
    /// </summary>
    public static string? ForPath(string? absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
        {
            return null;
        }

        // "/api/" statt StartsWith: die App kann unter einem Unterpfad liegen,
        // und dann steht vor dem api noch der Basispfad.
        var marker = absolutePath.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return null;
        }

        var tail = absolutePath[(marker + 5)..].Trim('/');
        return ByPath.TryGetValue(tail, out var store) ? store : null;
    }

    /// <summary>
    /// Die vier Behandlungstabellen. Nur sie koennen wartende Zeilen haben,
    /// also nur bei ihnen muss der Neuabruf etwas stehenlassen.
    /// </summary>
    public static bool HasPendingRows(string store)
        => store is CowTreatment or ClawTreatment or PlannedCowTreatment or PlannedClawTreatment;

    public static string StoreOf(MeadowEntityType type) => type switch
    {
        MeadowEntityType.CowTreatment => CowTreatment,
        MeadowEntityType.ClawTreatment => ClawTreatment,
        MeadowEntityType.PlannedCowTreatment => PlannedCowTreatment,
        _ => PlannedClawTreatment
    };

    /// <summary>
    /// Die Insert-Route. Alle vier sind serverseitig Upserts auf ClientId -
    /// nur deshalb darf die Outbox einen Eintrag wiederholen, dessen Antwort
    /// unterwegs verlorenging.
    /// </summary>
    public static string InsertRoute(MeadowEntityType type) => type switch
    {
        MeadowEntityType.CowTreatment => "api/cow-treatments/batch",
        MeadowEntityType.ClawTreatment => "api/claw-treatments",
        MeadowEntityType.PlannedCowTreatment => "api/planned-cow-treatments/batch",
        _ => "api/planned-claw-treatments"
    };

    /// <summary>
    /// Ob der Anfragerumpf eine LISTE ist.
    ///
    /// Die beiden Kuh-Routen nehmen einen Stapel, die beiden Klauen-Routen eine
    /// einzelne Zeile - das ist der heutige Zuschnitt der Naht und keine
    /// Entscheidung dieser Phase: ICowTreatmentService hat nur InsertRangeAsync,
    /// IClawTreatmentService nur InsertDataAsync.
    /// </summary>
    public static bool IsBatch(MeadowEntityType type)
        => type is MeadowEntityType.CowTreatment or MeadowEntityType.PlannedCowTreatment;

    public static MeadowDataKind KindOf(MeadowEntityType type) => type switch
    {
        MeadowEntityType.CowTreatment => MeadowDataKind.CowTreatment,
        MeadowEntityType.ClawTreatment => MeadowDataKind.ClawTreatment,
        MeadowEntityType.PlannedCowTreatment => MeadowDataKind.PlannedCowTreatment,
        _ => MeadowDataKind.PlannedClawTreatment
    };

    public static string Describe(MeadowEntityType type) => type switch
    {
        MeadowEntityType.CowTreatment => "Kuhbehandlung",
        MeadowEntityType.ClawTreatment => "Klauenbehandlung",
        MeadowEntityType.PlannedCowTreatment => "geplante Kuhbehandlung",
        _ => "geplante Klauenbehandlung"
    };
}
