using BB_Cow.Class;
using BB_Cow.Profile;

namespace BBCowDataLibrary.Tests.Profile;

/// <summary>
/// Builders for the cow-profile tests. Like KpiTestData: no database, no EF, no DI - the whole
/// aggregation layer was put in this assembly precisely so it can be tested this way.
///
/// Everything uses object initialisers rather than the positional constructors on purpose. The
/// ClawTreatment constructor takes its twelve claw fields in LV, LH, RV, RH order while
/// HoofPositions.All draws them LV, RV, LH, RH - a positional call here would be a coin flip.
/// </summary>
internal static class CowProfileTestData
{
    /// <summary>Fixed "today" for every window test, so boundaries are assertable.</summary>
    public static readonly DateTime Today = new(2026, 6, 15);

    /// <summary>The cow every test uses unless it needs a second one.</summary>
    public const string Mine = "DE 08 1523 0001";

    /// <summary>Deliberately a prefix of nothing and prefixed BY nothing - see Other.</summary>
    public const string Other = "DE 08 1523 0002";

    public static Cow Cow(
        string cowId = Mine,
        int collar = 101,
        string? earTag = null,
        bool isCalf = false,
        bool isGone = false)
        => new(cowId, earTag ?? (isCalf ? null : cowId), collar, isCalf, isGone);

    public static CowTreatment Treatment(
        DateTime? date = null,
        string cowId = Mine,
        int medicineId = 1,
        int udderId = int.MinValue,
        int? reasonId = null,
        float dosage = 10f,
        int id = 0)
        => new()
        {
            CowTreatmentId = id,
            EarTagNumber = cowId,
            MedicineId = medicineId,
            AdministrationDate = date ?? Today,
            MedicineDosage = dosage,
            WhereHowId = 1,
            UdderId = udderId,
            TreatmentReasonId = reasonId
        };

    /// <summary>
    /// A claw treatment with one hoof filled in. Use <see cref="Claw(DateTime?, string, bool, int)"/>
    /// plus <c>With</c> for anything touching several hoofs.
    /// </summary>
    public static ClawTreatment Claw(
        HoofPosition position,
        string finding = "Mortellaro",
        bool bandage = false,
        bool block = false,
        DateTime? date = null,
        string cowId = Mine,
        bool bandageRemoved = false,
        int id = 0)
        => Claw(date, cowId, bandageRemoved, id).With(position, finding, bandage, block);

    public static ClawTreatment Claw(
        DateTime? date = null,
        string cowId = Mine,
        bool bandageRemoved = false,
        int id = 0)
        => new()
        {
            ClawTreatmentId = id,
            EarTagNumber = cowId,
            TreatmentDate = date ?? Today,
            IsBandageRemoved = bandageRemoved
        };

    public static ClawTreatment With(
        this ClawTreatment treatment,
        HoofPosition position,
        string finding = "",
        bool bandage = false,
        bool block = false)
    {
        treatment.SetFinding(position, finding);
        treatment.SetBandage(position, bandage);
        treatment.SetBlock(position, block);
        return treatment;
    }

    public static PlannedCowTreatment PlannedCow(
        DateTime? date = null,
        string cowId = Mine,
        bool isTreated = false,
        int id = 0)
        => new()
        {
            PlannedCowTreatmentId = id,
            EarTagNumber = cowId,
            MedicineId = 1,
            AdministrationDate = date ?? Today,
            MedicineDosage = 10f,
            WhereHowId = 1,
            UdderId = int.MinValue,
            IsFound = false,
            IsTreatet = isTreated
        };

    public static PlannedClawTreatment PlannedClaw(
        DateTime? date = null,
        string cowId = Mine,
        int id = 0)
        => new()
        {
            PlannedClawTreatmentId = id,
            EarTagNumber = cowId,
            TreatmentDate = date ?? Today,
            ClawFindingLV = true
        };

    /// <summary>The four quarter combinations the tests need, keyed the way UdderService keys them.</summary>
    public static Dictionary<int, Udder> Udders() => new()
    {
        // (udderId, LV, LH, RV, RH) - note the constructor's order.
        [1] = new Udder(1, true, false, false, false),   // nur LV
        [2] = new Udder(2, true, true, true, true),      // alle vier
        [3] = new Udder(3, false, false, true, false),   // nur RV
        [9] = new Udder(9, false, false, false, false)   // "kein bestimmtes Viertel"
    };

    public static CowProfile Build(
        IEnumerable<CowTreatment>? cow = null,
        IEnumerable<ClawTreatment>? claw = null,
        IEnumerable<PlannedCowTreatment>? plannedCow = null,
        IEnumerable<PlannedClawTreatment>? plannedClaw = null,
        IReadOnlyDictionary<int, Udder>? udders = null,
        string cowId = Mine,
        DateTime? today = null)
        => CowProfileBuilder.Build(
            cowId,
            cow ?? Array.Empty<CowTreatment>(),
            claw ?? Array.Empty<ClawTreatment>(),
            plannedCow ?? Array.Empty<PlannedCowTreatment>(),
            plannedClaw ?? Array.Empty<PlannedClawTreatment>(),
            udders ?? Udders(),
            today ?? Today);
}
