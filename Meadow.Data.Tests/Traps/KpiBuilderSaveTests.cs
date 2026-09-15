using Meadow.Shared.Kpi;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Traps;

/// <summary>
/// Eine Baukasten-Kennzahl muss sich speichern lassen, obwohl sie kein Skript
/// hat.
///
/// Der Fehler, den diese Tests festhalten, war vollstaendig und betraf jede
/// einzelne Baukasten-Kennzahl: POST /api/kpis und PUT /api/kpis/{id} riefen
/// KpiScriptGuard.Reject unbedingt auf, das leere Skript einer
/// Baukasten-Kennzahl wurde mit "Das Skript ist leer." abgelehnt, und ueber die
/// Oberflaeche liess sich damit keine anlegen und keine aendern.
///
/// Warum das der Abnahme entgangen ist: die vier vorhandenen
/// Baukasten-Kennzahlen tragen ein Skript, weil DataSeeder es mitschreibt. Wer
/// nur die Kacheln anschaut, sieht nichts. Sichtbar wurde es erst beim
/// Anlegen einer neuen Kennzahl ueber den Dialog.
///
/// Der Waechter selbst bleibt unveraendert scharf - beim AUSFUEHREN gilt er
/// ohne Ausnahme, und ein Skript, das dasteht, wird auch im Baukasten geprueft.
/// </summary>
public class KpiBuilderSaveTests
{
    [Fact]
    public void A_builder_kpi_without_a_script_may_be_saved()
    {
        // Genau der Fall aus dem Dialog: Kind = Builder, Script nie gesetzt.
        Assert.Null(KpiScriptGuard.RejectForKind(KpiKind.Builder, null));
        Assert.Null(KpiScriptGuard.RejectForKind(KpiKind.Builder, ""));
        Assert.Null(KpiScriptGuard.RejectForKind(KpiKind.Builder, "   "));
    }

    [Fact]
    public void A_sql_kpi_without_a_script_is_still_refused()
    {
        // Die Gegenprobe. Ohne sie waere die Lockerung oben ein Loch: eine
        // Kennzahl im Expertenmodus OHNE Skript kann nichts ausrechnen.
        Assert.Equal("Das Skript ist leer.", KpiScriptGuard.RejectForKind(KpiKind.Sql, null));
        Assert.Equal("Das Skript ist leer.", KpiScriptGuard.RejectForKind(KpiKind.Sql, "  "));
    }

    [Fact]
    public void A_script_that_survives_the_switch_to_builder_mode_is_still_checked()
    {
        // KPIDialog loescht ein im Expertenmodus geschriebenes Skript NICHT,
        // damit der Wechsel umkehrbar bleibt. Ausgefuehrt wird es im Baukasten
        // nicht - aber es kann jederzeit wieder ausgefuehrt werden, und dann
        // muss es geprueft sein.
        var boese = "SELECT 1; DROP TABLE Cow_Treatment";

        Assert.NotNull(KpiScriptGuard.RejectForKind(KpiKind.Builder, boese));
        Assert.NotNull(KpiScriptGuard.RejectForKind(KpiKind.Sql, boese));
    }

    [Fact]
    public void A_harmless_script_passes_in_both_kinds()
    {
        var gut = "SELECT CAST(COUNT(*) AS CHAR) AS value FROM Cow_Treatment";

        Assert.Null(KpiScriptGuard.RejectForKind(KpiKind.Builder, gut));
        Assert.Null(KpiScriptGuard.RejectForKind(KpiKind.Sql, gut));
    }
}
