using BB_Cow.Class;

namespace _4Cows_FE.Components.Meadow;

/// <summary>
/// Eine Zeile der Verbaende-Tabelle: ein Verband an einer Klaue.
///
/// Das Design zeichnet eine Zeile pro Klaue, die Datenbank kennt aber nur
/// ein gemeinsames IsBandageRemoved pro Behandlung. Deshalb ist das eine
/// reine Projektion - und deshalb muss die Entfernen-Bestaetigung die
/// Geschwister-Klauen derselben Behandlung benennen.
/// </summary>
public sealed record BandagedClaw(
    int TreatmentId,
    string CowId,
    HoofPosition Position,
    string Finding,
    bool HasBlock,
    DateTime Since)
{
    public string Key => $"{TreatmentId}:{Position}";

    public int Days => (DateTime.Today - Since.Date).Days;

    public bool IsOverdue => Days >= 7;

    /// <summary>"LV · Vorne links"</summary>
    public string HoofText => $"{Position} · {HoofPositions.SideLabel(Position)}";

    public static IReadOnlyList<BandagedClaw> Project(IEnumerable<ClawTreatment> withBandage)
        => withBandage
            .SelectMany(t => HoofPositions.All
                .Where(t.GetBandage)
                .Select(p => new BandagedClaw(
                    t.ClawTreatmentId,
                    t.EarTagNumber,
                    p,
                    t.GetFinding(p)?.Trim() ?? string.Empty,
                    t.GetBlock(p),
                    t.TreatmentDate)))
            .ToList();
}
