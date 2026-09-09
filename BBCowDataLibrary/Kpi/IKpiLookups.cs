using BB_Cow.Class;

namespace BB_Cow.Kpi;

/// <summary>
/// Everything <see cref="KpiSourceRegistry"/> needs to project rows: the four treatment tables plus
/// the cows, and the four name lookups.
///
/// This is an interface rather than the services themselves because the real services take an
/// IDbContextFactory and cannot be constructed in a unit test. With a fake implementation the row
/// projection - including the Ear_Tag_Number-holds-Cow_ID resolution and the udder sentinel - is
/// testable without a database.
/// </summary>
public interface IKpiLookups
{
    /// <summary>Collar number as text, or empty when the cow is unknown.</summary>
    string CollarLabel(string cowId);

    string MedicineName(int medicineId);

    /// <summary>
    /// Dosiereinheit des Medikaments ("ml", "Stueck"), oder leer wenn keine
    /// hinterlegt ist.
    ///
    /// Wird gebraucht, damit SumDosage/AvgDosage bemerken kann, dass die
    /// beitragenden Zeilen mehr als eine Einheit umfassen. Bis es die
    /// Dosiereinheit am Medikament gab, war jede Menge implizit ml und eine
    /// Summe darueber sinnvoll; ohne diese Pruefung waere sie das jetzt nicht
    /// mehr - und zwar unbemerkt.
    /// </summary>
    string MedicineDosageUnit(int medicineId);

    string WhereHowName(int whereHowId);

    /// <summary>
    /// The udder quarters as one label ("LV/ RH", "Alle 4"), and EMPTY for the all-false sentinel
    /// row. That empty string is what replaces the hardcoded "WHERE UDDER_ID != 16": a row with no
    /// quarter set simply has no group value, so Top-1 skips it without knowing any id.
    /// </summary>
    string UdderLabel(int udderId);

    IEnumerable<Cow> Cows { get; }

    IEnumerable<CowTreatment> CowTreatments { get; }

    IEnumerable<ClawTreatment> ClawTreatments { get; }

    IEnumerable<PlannedCowTreatment> PlannedCowTreatments { get; }

    IEnumerable<PlannedClawTreatment> PlannedClawTreatments { get; }
}
