namespace Meadow.Shared.Models;

public enum TreatmentPlanRunStatus
{
    /// <summary>Offen, heute nichts zu tun.</summary>
    Upcoming,

    /// <summary>Heute ist eine Gabe faellig.</summary>
    DueToday,

    /// <summary>Nur Interval: Faelligkeit ueberschritten.</summary>
    Overdue,

    /// <summary>Alle Gaben erhalten.</summary>
    Completed,

    /// <summary>Nur Window: Fenster vorbei, nicht alle Gaben erhalten.</summary>
    Incomplete,

    /// <summary>Manuell abgebrochen.</summary>
    Cancelled
}

/// <summary>
/// Ein berechneter Durchlauf eines Behandlungsplans fuer eine Kuh. Momentaufnahme
/// zu einem "heute" - Neuberechnung heisst neues Objekt.
/// </summary>
public sealed record TreatmentPlanRun
{
    public required string CowId { get; init; }
    public required int MedicineId { get; init; }
    public required Guid StartTreatmentClientId { get; init; }
    public required TreatmentPlanMode Mode { get; init; }
    public required int DoseCount { get; init; }
    public required int PlanDays { get; init; }

    /// <summary>Die gezaehlten Gaben, aufsteigend nach Datum. Nie leer.</summary>
    public required IReadOnlyList<CowTreatment> Doses { get; init; }

    public required TreatmentPlanRunStatus Status { get; init; }

    /// <summary>Nur Interval und nur solange aktiv: Tag der naechsten Gabe.</summary>
    public DateTime? NextDueDate { get; init; }

    /// <summary>Nur Window: letzter zulaessiger Tag (einschliesslich).</summary>
    public DateTime? WindowEnd { get; init; }

    public TreatmentPlanCancellation? Cancellation { get; init; }

    public DateTime StartDate => Doses[0].AdministrationDate.Date;
    public int DosesDone => Doses.Count;

    public bool IsActive => Status is TreatmentPlanRunStatus.Upcoming
        or TreatmentPlanRunStatus.DueToday
        or TreatmentPlanRunStatus.Overdue;

    public bool IsDue => Status is TreatmentPlanRunStatus.DueToday or TreatmentPlanRunStatus.Overdue;

    public int OverdueDays(DateTime today)
        => NextDueDate is null ? 0 : Math.Max(0, (today.Date - NextDueDate.Value.Date).Days);
}
