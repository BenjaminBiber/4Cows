namespace Meadow.Shared.Models;

/// <summary>
/// Regeln zur Plan-Definition am <see cref="Medicine"/>. Rein und paketfrei,
/// damit Dialog (Client), Endpunkt und Service (Server) dieselbe Regel nutzen.
/// </summary>
public static class TreatmentPlanDefinition
{
    public const int MinDoses = 2;
    public const int MaxDoses = 20;
    public const int MinDays = 1;
    public const int MaxDays = 90;

    /// <summary>
    /// <c>null</c> = gueltig. "Kein Plan" (alles null) ist gueltig; ein halb
    /// ausgefuellter Plan nicht.
    /// </summary>
    public static string? Validate(int? doses, TreatmentPlanMode? mode, int? days)
    {
        if (doses is null && mode is null && days is null)
        {
            return null;
        }

        if (doses is null || mode is null || days is null)
        {
            return "Behandlungsplan unvollständig: Anzahl, Art und Tage angeben.";
        }

        if (!Enum.IsDefined(mode.Value))
        {
            return "Unbekannte Art des Behandlungsplans.";
        }

        if (doses < MinDoses || doses > MaxDoses)
        {
            return $"Die Anzahl der Gaben muss zwischen {MinDoses} und {MaxDoses} liegen.";
        }

        if (days < MinDays || days > MaxDays)
        {
            return $"Die Tage müssen zwischen {MinDays} und {MaxDays} liegen.";
        }

        return null;
    }

    public static bool IsDefined(Medicine medicine)
        => medicine.PlanDoseCount is not null
           && Validate(medicine.PlanDoseCount, medicine.PlanMode, medicine.PlanDays) is null;

    public static string? Describe(Medicine medicine)
    {
        if (!IsDefined(medicine))
        {
            return null;
        }

        var doses = medicine.PlanDoseCount!.Value;
        var days = medicine.PlanDays!.Value;
        return medicine.PlanMode == TreatmentPlanMode.Window
            ? $"{doses}× innerhalb von {(days == 1 ? "1 Tag" : $"{days} Tagen")}"
            : days == 1 ? $"{doses}× täglich" : $"{doses}× alle {days} Tage";
    }

    /// <summary>
    /// Stichtag nach dem Speichern. Kein Plan -> null. Plan neu (vorher keiner
    /// oder Insert) -> heute. Plan bestand schon -> bisheriger Stichtag. Ein vom
    /// Client mitgeschickter Wert in <paramref name="after"/> wird ignoriert.
    /// </summary>
    public static DateTime? ResolveValidFrom(Medicine? before, Medicine after, DateTime today)
    {
        if (!IsDefined(after))
        {
            return null;
        }

        if (before is not null && IsDefined(before) && before.PlanValidFrom is not null)
        {
            return before.PlanValidFrom;
        }

        return today.Date;
    }
}
