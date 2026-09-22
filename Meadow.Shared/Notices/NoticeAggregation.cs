using Meadow.Shared.Models;

namespace Meadow.Shared.Notices;

/// <summary>
/// Die reine Zusammenfuehrung mehrerer Hinweisquellen zu EINER anzeigefertigen
/// Liste. Rumpf hier, damit sie ohne Client, ohne DI und ohne bUnit pruefbar ist
/// - die Ordner-Nachbarn in Meadow.Shared/Lookups folgen demselben Muster.
///
/// Zwei Zusicherungen, auf die sich Anzeige (Task 4) und Push (Task 6) verlassen:
/// <list type="bullet">
///   <item><b>Dedup nach Id:</b> jeder Hinweis steht genau einmal. Liefern zwei
///     Quellen denselben Sachverhalt (gleiche <see cref="MeadowNotice.Id"/>),
///     ueberlebt der zuerst gesehene - so kann eine Quelle einen fremden
///     Hinweis nicht ueberschreiben.</item>
///   <item><b>Determinismus:</b> gleiche Eingabe, gleiche Ausgabe -
///     Sortierung nach Rang (dringend zuerst), dann nach Id. Ohne feste zweite
///     Stufe waere die Reihenfolge gleichrangiger Hinweise von der
///     Provider-Reihenfolge abhaengig und die Anzeige wuerde bei jeder
///     Neuberechnung flackern.</item>
/// </list>
/// </summary>
public static class NoticeAggregation
{
    /// <summary>
    /// Fuehrt die Hinweislisten mehrerer Quellen zusammen: dedupliziert nach
    /// <see cref="MeadowNotice.Id"/> und sortiert deterministisch (Rang
    /// absteigend, dann Id aufsteigend).
    ///
    /// <paramref name="sources"/> ist bewusst eine Liste von Listen - genau das,
    /// was <c>MeadowNoticeState</c> beim Einsammeln der Provider erhaelt. Ein
    /// <c>null</c>-Eintrag (eine Quelle ohne Hinweise) wird uebersprungen, damit
    /// der Aufrufer nicht vorab filtern muss.
    /// </summary>
    public static IReadOnlyList<MeadowNotice> Aggregate(IEnumerable<IEnumerable<MeadowNotice>?> sources)
    {
        // Reihenfolge merken: Dedup soll den ZUERST gesehenen Hinweis behalten,
        // nicht irgendeinen. Ein Dictionary allein gibt keine Einfuegereihenfolge
        // zu, aber die brauchen wir gar nicht als Endreihenfolge - sortiert wird
        // ohnehin. Wir brauchen sie nur, um bei gleicher Id den ersten zu nehmen.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<MeadowNotice>();

        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            foreach (var notice in source)
            {
                if (notice is null)
                {
                    continue;
                }

                if (seen.Add(notice.Id))
                {
                    unique.Add(notice);
                }
            }
        }

        // Rang absteigend: Critical (hoechster Enum-Wert) nach oben. Zweite Stufe
        // Id ordinal, damit gleichrangige Hinweise eine feste Reihenfolge haben
        // und die Ausgabe unabhaengig von der Provider-Reihenfolge ist.
        return unique
            .OrderByDescending(n => n.Severity)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();
    }
}
