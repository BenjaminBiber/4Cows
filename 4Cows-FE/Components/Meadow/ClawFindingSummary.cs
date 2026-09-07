using System.Text;
using BB_Cow.Class;

namespace _4Cows_FE.Components.Meadow;

/// <summary>
/// Baut die Spalte "Befunde" der Klauen-Tabelle aus den zwoelf flachen
/// Feldern einer Klauenbehandlung, z.B.
/// "Mortellaro · Sohlengeschwür · 2 Verbände".
///
/// Die Zusammenfassung ist auch das Suchfeld dieser Tabelle - sie wird
/// deshalb einmal pro Datenladen vorberechnet, nicht pro Tastendruck.
/// </summary>
public static class ClawFindingSummary
{
    private const int MaxFindingSegments = 3;

    public static string Build(ClawTreatment treatment, string fallbackFinding)
    {
        var segments = new List<string>();

        // 1. Befunde sammeln, case-insensitiv nach Erstauftreten gruppieren.
        var groups = new List<(string Finding, List<HoofPosition> Positions)>();
        foreach (var position in HoofPositions.All)
        {
            var finding = treatment.GetFinding(position)?.Trim();
            if (string.IsNullOrEmpty(finding))
            {
                continue;
            }

            var group = groups.FirstOrDefault(
                g => string.Equals(g.Finding, finding, StringComparison.OrdinalIgnoreCase));

            if (group.Positions is null)
            {
                groups.Add((finding, new List<HoofPosition> { position }));
            }
            else
            {
                group.Positions.Add(position);
            }
        }

        // 2. Ein Befund ueber alle vier Klauen, ein Befund ueber 1-3 Klauen
        //    mit Positionen, mehrere Befunde ohne Positionen (haelt es kurz).
        if (groups.Count == 1)
        {
            var (finding, positions) = groups[0];
            segments.Add(finding);
            segments.Add(positions.Count == HoofPositions.All.Length
                ? "alle 4 Klauen"
                : string.Join("/", positions));

            // "Pflegeschnitt" + "alle 4 Klauen" bzw. "Sohlengeschwür" + "LH"
            if (positions.Count != HoofPositions.All.Length)
            {
                segments[0] = $"{finding} {string.Join("/", positions)}";
                segments.RemoveAt(1);
            }
        }
        else if (groups.Count > 1)
        {
            foreach (var (finding, _) in groups.Take(MaxFindingSegments))
            {
                segments.Add(finding);
            }

            var overflow = groups.Count - MaxFindingSegments;
            if (overflow > 0)
            {
                segments.Add($"+{overflow} weitere");
            }
        }

        // 3. Verband- und Klotz-Segment.
        var bandaged = HoofPositions.All.Where(treatment.GetBandage).ToList();
        if (bandaged.Count > 0)
        {
            segments.Add(treatment.IsBandageRemoved
                ? "Verband entfernt"
                : bandaged.Count == 1
                    ? $"Verband {bandaged[0]}"
                    : $"{bandaged.Count} Verbände");
        }

        var blocked = HoofPositions.All.Where(treatment.GetBlock).ToList();
        if (blocked.Count > 0)
        {
            segments.Add(blocked.Count == 1 ? $"Klotz {blocked[0]}" : $"{blocked.Count} Klötze");
        }

        // 4. Gar nichts erfasst -> der pflegbare Standardwert.
        return segments.Count == 0
            ? fallbackFinding
            : string.Join(" · ", segments);
    }
}
