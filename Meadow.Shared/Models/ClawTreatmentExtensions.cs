namespace BB_Cow.Class;

/// <summary>
/// Positionsbasierter Zugriff auf die zwoelf flachen Felder von
/// ClawTreatment (Claw_Finding_LV_ID, Bandage_LV, Block_LV, ... ) und die vier
/// Booleans von PlannedClawTreatment.
///
/// Ohne Schema- oder Spaltenaenderung. Damit werden aus vier if/else-Zweigen
/// im Hinzufuegen-Dialog und 24 Verzweigungen im Anzeige-Dialog je eine
/// Schleife.
/// </summary>
public static class ClawTreatmentExtensions
{
    /// <summary>
    /// Die Befund-ID dieser Klaue. <c>null</c> heisst "an dieser Klaue wurde
    /// nichts erfasst"; den Anzeigenamen loest ClawFindingService.GetNameById
    /// auf. Bis zur Migration AddClawFinding stand hier der Befundtext selbst.
    /// </summary>
    public static int? GetFindingId(this ClawTreatment t, HoofPosition p) => p switch
    {
        HoofPosition.LV => t.ClawFindingLVId,
        HoofPosition.RV => t.ClawFindingRVId,
        HoofPosition.LH => t.ClawFindingLHId,
        _ => t.ClawFindingRHId
    };

    public static void SetFindingId(this ClawTreatment t, HoofPosition p, int? value)
    {
        switch (p)
        {
            case HoofPosition.LV: t.ClawFindingLVId = value; break;
            case HoofPosition.RV: t.ClawFindingRVId = value; break;
            case HoofPosition.LH: t.ClawFindingLHId = value; break;
            default: t.ClawFindingRHId = value; break;
        }
    }

    public static bool GetBandage(this ClawTreatment t, HoofPosition p) => p switch
    {
        HoofPosition.LV => t.BandageLV,
        HoofPosition.RV => t.BandageRV,
        HoofPosition.LH => t.BandageLH,
        _ => t.BandageRH
    };

    public static void SetBandage(this ClawTreatment t, HoofPosition p, bool value)
    {
        switch (p)
        {
            case HoofPosition.LV: t.BandageLV = value; break;
            case HoofPosition.RV: t.BandageRV = value; break;
            case HoofPosition.LH: t.BandageLH = value; break;
            default: t.BandageRH = value; break;
        }
    }

    public static bool GetBlock(this ClawTreatment t, HoofPosition p) => p switch
    {
        HoofPosition.LV => t.BlockLV,
        HoofPosition.RV => t.BlockRV,
        HoofPosition.LH => t.BlockLH,
        _ => t.BlockRH
    };

    public static void SetBlock(this ClawTreatment t, HoofPosition p, bool value)
    {
        switch (p)
        {
            case HoofPosition.LV: t.BlockLV = value; break;
            case HoofPosition.RV: t.BlockRV = value; break;
            case HoofPosition.LH: t.BlockLH = value; break;
            default: t.BlockRH = value; break;
        }
    }

    /// <summary>Wurde an dieser Klaue ueberhaupt etwas erfasst?</summary>
    public static bool HasData(this ClawTreatment t, HoofPosition p)
        => t.GetFindingId(p) is not null || t.GetBandage(p) || t.GetBlock(p);

    /// <summary>
    /// Die Klauen dieser Behandlung, an denen ein Verband noch LIEGT.
    ///
    /// IsBandageRemoved gilt behandlungsweit, nicht je Klaue - wer das
    /// vergisst, zaehlt abgenommene Verbaende mit. Steht deshalb hier und
    /// nicht ein viertes Mal verstreut ueber BandagedClaw.Project,
    /// ClawTreatmentService.GetClawTreatmentsWithBandage und den
    /// CowProfileBuilder: die Zahl auf der Kachel und die Zeilen in der
    /// Verbaende-Tabelle muessen dieselbe Menge sein.
    /// </summary>
    public static IEnumerable<HoofPosition> OpenBandages(this ClawTreatment t)
        => t.IsBandageRemoved
            ? Enumerable.Empty<HoofPosition>()
            : HoofPositions.All.Where(t.GetBandage);

    public static bool GetFlag(this PlannedClawTreatment t, HoofPosition p) => p switch
    {
        HoofPosition.LV => t.ClawFindingLV,
        HoofPosition.RV => t.ClawFindingRV,
        HoofPosition.LH => t.ClawFindingLH,
        _ => t.ClawFindingRH
    };

    public static void SetFlag(this PlannedClawTreatment t, HoofPosition p, bool value)
    {
        switch (p)
        {
            case HoofPosition.LV: t.ClawFindingLV = value; break;
            case HoofPosition.RV: t.ClawFindingRV = value; break;
            case HoofPosition.LH: t.ClawFindingLH = value; break;
            default: t.ClawFindingRH = value; break;
        }
    }
}
