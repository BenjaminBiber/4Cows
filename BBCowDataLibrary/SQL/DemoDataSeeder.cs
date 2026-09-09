using BB_Cow.Class;
using BB_Cow.Services;
using Microsoft.EntityFrameworkCore;

namespace BBCowDataLibrary.SQL;

/// <summary>
/// Fachliche Beispieldaten fuer die oeffentliche Demo-Instanz (Demo:Enabled).
/// Laeuft beim Start nach DataSeeder und danach jede Nacht ueber
/// DemoResetBackgroundService.
///
/// Bewusst am DatabaseContext geschrieben und nicht ueber die Services, so
/// wie DataSeeder es auch tut. Ginge der Seeder ueber ClawTreatmentService,
/// haengte dessen InsertDataAsync jeden rohen Befundwert an
/// _cachedClawFindingList - die leeren Befunde unberuehrter Klauen landeten
/// dann als Leereintraege in der Autocomplete-Liste. Die Caches werden
/// stattdessen nach dem Seeden komplett neu geladen.
///
/// Alle Zufallswerte kommen aus einem fest geseedeten Random: derselbe
/// Datenbestand bei jedem Reset, also reproduzierbare Screenshots. Gleiche
/// Begruendung wie bei der Diagramm-Palette in Index.razor.
/// </summary>
public static class DemoDataSeeder
{
    private const int Seed = 4711;

    /// <summary>Ohrmarken im Format des Platzhalters aus den Dialogen.</summary>
    private const string EarTagPrefix = "DE 08 1523 ";

    private static readonly string[] MedicineNames =
    {
        "Ubrolexin", "Metacam", "Cobactan", "Vetrimoxin", "Novaminsulfon", "Resflor"
    };

    /// <summary>
    /// Wie/Wo. "IZ" (intrazitzenal) ist der einzige Wert, den das Projekt
    /// historisch kennt - das V2-V3-Transferskript legt ihn mit
    /// ShowDialog = TRUE an, weil dabei ein Euterviertel gewaehlt wird.
    /// Alles andere ist eine Verabreichungsart ohne Viertelbezug.
    /// </summary>
    private static readonly (string Name, bool ShowDialog)[] WhereHowNames =
    {
        ("IZ", true), ("i.m.", false), ("s.c.", false),
        ("i.v.", false), ("oral", false), ("lokal", false)
    };

    /// <summary>
    /// Beispielgruende NUR fuer die Demo. Produktive Installationen starten mit
    /// leerer Liste - DataSeeder legt hier bewusst nichts an, damit kein
    /// Betrieb fremde Begriffe wegraeumen muss.
    /// </summary>
    private static readonly string[] TreatmentReasonNames =
    {
        "Mastitis", "Lahmheit", "Fieber", "Nachgeburtsverhaltung",
        "Trockenstellen", "Impfung"
    };

    /// <summary>
    /// Befunde aus dem Vokabular, das die App selbst verwendet (Platzhalter
    /// im ClawSelector, ClawFindingSummary, DataSeeder-Fallback). Die Spalten
    /// Claw_Finding_* sind varchar(32) - laengere Formulierungen kippen den
    /// Insert. Und weil ClawTreatmentService.ClawFindingList die
    /// Autocomplete-Vorschlaege aus dem Bestand aufbaut, wird genau diese
    /// Liste zum Vorschlagsvokabular der Demo.
    /// </summary>
    private static readonly string[] ClawFindings =
    {
        "Mortellaro", "Mortellaro (M2)", "Sohlengeschwür",
        "Pflegeschnitt", "Pflege", "Weiße-Linie-Defekt", "Ballenhornfäule"
    };

    private static readonly string[] PlannedClawNotes =
    {
        "Kontrolle nach Verband", "Routineschnitt fällig", "Lahmheit beobachtet",
        "Nachkontrolle Mortellaro", "Klotz erneuern"
    };

    public static async Task SeedAsync(DatabaseContext context)
    {
        // Reihenfolge zaehlt: die Behandlungen brauchen die IDs der drei
        // Nachschlagetabellen.
        var udders = await EnsureUddersAsync(context);
        var medicineIds = await EnsureMedicinesAsync(context);
        var whereHows = await EnsureWhereHowsAsync(context);
        var reasonIds = await EnsureTreatmentReasonsAsync(context);

        await SeedHerdAndTreatmentsAsync(context, udders, medicineIds, whereHows, reasonIds);
    }

    // ---- Nachschlagetabellen -------------------------------------------

    /// <summary>
    /// Die 16 Viertel-Kombinationen. Der naechtliche Reset leert diese
    /// Tabelle bewusst NICHT, der Guard hier greift also dauerhaft.
    ///
    /// Die Einfuegereihenfolge ist nicht beliebig: das mitgelieferte KPI
    /// "Meist behandeltes Viertel" filtert hart auf WHERE c.UDDER_ID != 16
    /// und meint damit die Zeile "kein Viertel gewaehlt". Historisch entsteht
    /// diese ID aus dem 16-fachen Cross Join des V2-V3-Transferskripts, bei
    /// dem TRUE vor FALSE steht - all-false ist dort die letzte Zeile. Die
    /// Schleifen unten bilden das nach.
    /// </summary>
    private static async Task<Dictionary<(bool, bool, bool, bool), int>> EnsureUddersAsync(
        DatabaseContext context)
    {
        if (!await context.Udders.AnyAsync())
        {
            var combinations = new List<Udder>(16);

            foreach (var lv in new[] { true, false })
            foreach (var lh in new[] { true, false })
            foreach (var rv in new[] { true, false })
            foreach (var rh in new[] { true, false })
            {
                // Der vollstaendige Konstruktor mit udderId: 0, NICHT der
                // parameterlose: der setzt UdderId auf int.MinValue (siehe
                // Udder.cs) statt auf den CLR-Default. EF haelt einen
                // gesetzten Schluessel fuer vergeben und sieht dann 16 Mal
                // dieselbe Entitaet - AddRangeAsync wirft dabei.
                combinations.Add(new Udder(0, lv, lh, rv, rh));
            }

            await context.Udders.AddRangeAsync(combinations);
            await context.SaveChangesAsync();
        }

        var byQuarters = (await context.Udders.AsNoTracking().ToListAsync())
            .ToDictionary(
                u => (u.QuarterLV, u.QuarterLH, u.QuarterRV, u.QuarterRH),
                u => u.UdderId);

        // Hier stand eine Warnung, falls die all-false-Zeile nicht auf ID 16
        // liegt: das mitgelieferte KPI "Meist behandeltes Viertel" filterte
        // hart auf "WHERE c.UDDER_ID != 16" und zeigte sonst still Unsinn.
        //
        // Der Filter existiert nicht mehr. Das KPI ist eine Builder-Definition,
        // und die Zeilen-Projektion erkennt die Zeile daran, dass alle vier
        // Viertel false sind - sie bekommt dann gar keinen Gruppenwert und
        // faellt von selbst aus der Auswertung. Keine ID mehr im Spiel, also
        // auch nichts mehr zu warnen.

        return byQuarters;
    }

    private static async Task<List<int>> EnsureMedicinesAsync(DatabaseContext context)
    {
        if (!await context.Medicines.AnyAsync())
        {
            await context.Medicines.AddRangeAsync(MedicineNames.Select(n => new Medicine(0, n)));
            await context.SaveChangesAsync();
        }

        return await context.Medicines.AsNoTracking()
            .Select(m => m.MedicineId)
            .ToListAsync();
    }

    private static async Task<List<WhereHow>> EnsureWhereHowsAsync(DatabaseContext context)
    {
        if (!await context.WhereHows.AnyAsync())
        {
            await context.WhereHows.AddRangeAsync(
                WhereHowNames.Select(w => new WhereHow(0, w.Name, w.ShowDialog)));
            await context.SaveChangesAsync();
        }

        return await context.WhereHows.AsNoTracking().ToListAsync();
    }

    private static async Task<List<int>> EnsureTreatmentReasonsAsync(DatabaseContext context)
    {
        if (!await context.TreatmentReasons.AnyAsync())
        {
            await context.TreatmentReasons.AddRangeAsync(
                TreatmentReasonNames.Select(n => new TreatmentReason(0, n)));
            await context.SaveChangesAsync();
        }

        return await context.TreatmentReasons.AsNoTracking()
            .Select(r => r.TreatmentReasonId)
            .ToListAsync();
    }

    // ---- Bestand und Behandlungen --------------------------------------

    private static async Task SeedHerdAndTreatmentsAsync(
        DatabaseContext context,
        IReadOnlyDictionary<(bool, bool, bool, bool), int> udders,
        IReadOnlyList<int> medicineIds,
        IReadOnlyList<WhereHow> whereHows,
        IReadOnlyList<int> reasonIds)
    {
        if (await context.Cows.AnyAsync())
        {
            return;
        }

        var random = new Random(Seed);
        var today = DateTime.Today;

        var cows = BuildHerd(random);
        await context.Cows.AddRangeAsync(cows);
        await context.SaveChangesAsync();

        // Behandlungen laufen nur auf anwesende Tiere mit Ohrmarke: die
        // Kuh-Auswahl in den Dialogen filtert IsGone heraus, und die vier
        // Behandlungstabellen speichern in Ear_Tag_Number die Cow_ID.
        var active = cows
            .Where(c => !c.IsGone && !c.IsCalv)
            .Select(c => c.CowId)
            .ToList();

        await context.CowTreatments.AddRangeAsync(
            BuildCowTreatments(random, today, active, medicineIds, whereHows, udders, reasonIds));

        await context.ClawTreatments.AddRangeAsync(
            BuildClawTreatments(random, today, active));

        await context.PlannedCowTreatments.AddRangeAsync(
            BuildPlannedCowTreatments(random, today, active, medicineIds, whereHows, udders, reasonIds));

        await context.PlannedClawTreatments.AddRangeAsync(
            BuildPlannedClawTreatments(random, today, active));

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 40 Tiere: 34 anwesend mit Ohrmarke, 3 abgegangen, 3 Kaelber ohne
    /// Ohrmarke. Fuer identifizierte Tiere gilt Cow_ID == Ear_Tag_Number -
    /// die Behandlungstabellen speichern die Cow_ID in einer Spalte, die
    /// Ear_Tag_Number heisst, und Datenbanken aus dem alten
    /// Installationsskript haben darauf noch einen Fremdschluessel auf
    /// Cow(Ear_Tag_Number).
    /// </summary>
    private static List<Cow> BuildHerd(Random random)
    {
        var cows = new List<Cow>(40);
        var collar = 101;

        for (var i = 1; i <= 37; i++)
        {
            var earTag = $"{EarTagPrefix}{1000 + i * 7:0000}";
            cows.Add(new Cow(earTag, collar, isGone: i > 34));
            collar += 1 + random.Next(2);
        }

        for (var i = 0; i < 3; i++)
        {
            // Nicht Cow.CreateCalf: das zieht eine echte Guid.NewGuid, und die
            // Cow_ID der Kaelber waere damit als einziges Feld nach jedem
            // naechtlichen Reset eine andere. Hier stammen die 16 Bytes aus
            // demselben geseedeten Random wie alles andere, der Bestand ist
            // also wirklich reproduzierbar.
            var id = new byte[16];
            random.NextBytes(id);
            cows.Add(new Cow(new Guid(id).ToString(), null, collar, isCalv: true, isGone: false));
            collar += 1 + random.Next(2);
        }

        return cows;
    }

    /// <summary>
    /// Rund 120 Kuhbehandlungen ueber 14 Monate, absichtlich ungleich
    /// verteilt - ein Jahresdiagramm mit zwoelf gleich hohen Balken sieht
    /// nach Testdaten aus.
    /// </summary>
    private static List<CowTreatment> BuildCowTreatments(
        Random random,
        DateTime today,
        IReadOnlyList<string> cowIds,
        IReadOnlyList<int> medicineIds,
        IReadOnlyList<WhereHow> whereHows,
        IReadOnlyDictionary<(bool, bool, bool, bool), int> udders,
        IReadOnlyList<int> reasonIds)
    {
        var noQuarter = udders[(false, false, false, false)];
        var quarterIds = QuarterIds(udders);

        var treatments = new List<CowTreatment>();

        foreach (var date in SpreadOverMonths(random, today, months: 14, total: 120))
        {
            var whereHow = whereHows[random.Next(whereHows.Count)];

            // Ein Viertel wird in der App nur bei ShowDialog ueberhaupt
            // gewaehlt; alles andere bekommt die "kein Viertel"-Zeile. Nur
            // dadurch hat das KPI "Meist behandeltes Viertel" ueberhaupt
            // etwas zu zaehlen.
            var udderId = whereHow.ShowDialog
                ? quarterIds[random.Next(quarterIds.Count)]
                : noQuarter;

            // Rund 70 Prozent mit Grund. Der Rest bleibt bewusst leer: nur so
            // zeigt die Demo das "–" in der Spalte, und die Filteroption
            // "Ohne Grund" findet ueberhaupt etwas.
            int? reasonId = random.Next(100) < 70
                ? reasonIds[random.Next(reasonIds.Count)]
                : null;

            treatments.Add(new CowTreatment(
                0,
                cowIds[random.Next(cowIds.Count)],
                medicineIds[random.Next(medicineIds.Count)],
                date,
                Dosage(random),
                whereHow.WhereHowId,
                udderId,
                reasonId));
        }

        return treatments;
    }

    /// <summary>
    /// Rund 80 Klauenbehandlungen. Zehn davon liegen 2 bis 14 Tage zurueck
    /// und haben einen NICHT abgenommenen Verband - nur solche Zeilen zeigt
    /// die Seite Verbaende (ClawTreatmentService.GetClawTreatmentsWithBandage
    /// filtert auf Bandage* und !IsBandageRemoved), und ab sieben Tagen
    /// greift dort der Chip fuer ueberfaellige. Bei allen aelteren ist der
    /// Verband ab, sonst stuende die Liste voll mit Behandlungen von vor
    /// einem Jahr.
    /// </summary>
    private static List<ClawTreatment> BuildClawTreatments(
        Random random,
        DateTime today,
        IReadOnlyList<string> cowIds)
    {
        var treatments = new List<ClawTreatment>();

        foreach (var date in SpreadOverMonths(random, today, months: 14, total: 70))
        {
            treatments.Add(BuildClawTreatment(random, cowIds, date, bandageOpen: false));
        }

        for (var i = 0; i < 10; i++)
        {
            treatments.Add(BuildClawTreatment(
                random, cowIds, today.AddDays(-(2 + random.Next(13))), bandageOpen: true));
        }

        return treatments;
    }

    private static ClawTreatment BuildClawTreatment(
        Random random, IReadOnlyList<string> cowIds, DateTime date, bool bandageOpen)
    {
        var treatment = new ClawTreatment
        {
            EarTagNumber = cowIds[random.Next(cowIds.Count)],
            TreatmentDate = date,
            // Die Spalten sind NOT NULL, aber Leerstring ist erlaubt und
            // bedeutet "an dieser Klaue wurde nichts erfasst" - genau so
            // speichert es auch der Hinzufuegen-Dialog.
            ClawFindingLV = string.Empty,
            ClawFindingRV = string.Empty,
            ClawFindingLH = string.Empty,
            ClawFindingRH = string.Empty
        };

        // Eine bis drei betroffene Klauen, der Rest bleibt leer.
        var positions = HoofPositions.All
            .OrderBy(_ => random.Next())
            .Take(1 + random.Next(3))
            .ToList();

        var bandaged = false;

        foreach (var position in positions)
        {
            treatment.SetFinding(position, ClawFindings[random.Next(ClawFindings.Length)]);

            if (random.Next(100) < 45)
            {
                treatment.SetBandage(position, true);
                bandaged = true;
            }

            if (random.Next(100) < 30)
            {
                treatment.SetBlock(position, true);
            }
        }

        if (bandageOpen)
        {
            // Mindestens ein Verband, sonst taucht die Zeile auf der
            // Verbaende-Seite gar nicht erst auf.
            treatment.SetBandage(positions[0], true);
            treatment.IsBandageRemoved = false;
        }
        else
        {
            treatment.IsBandageRemoved = bandaged;
        }

        return treatment;
    }

    /// <summary>
    /// 15 geplante Kuhbehandlungen rund um heute: teils noch offen, teils
    /// gefunden, teils gefunden und behandelt.
    /// </summary>
    private static List<PlannedCowTreatment> BuildPlannedCowTreatments(
        Random random,
        DateTime today,
        IReadOnlyList<string> cowIds,
        IReadOnlyList<int> medicineIds,
        IReadOnlyList<WhereHow> whereHows,
        IReadOnlyDictionary<(bool, bool, bool, bool), int> udders,
        IReadOnlyList<int> reasonIds)
    {
        var noQuarter = udders[(false, false, false, false)];
        var quarterIds = QuarterIds(udders);

        var planned = new List<PlannedCowTreatment>(15);

        for (var i = 0; i < 15; i++)
        {
            var whereHow = whereHows[random.Next(whereHows.Count)];
            var isFound = random.Next(100) < 60;

            // Rund 60 Prozent mit Grund - siehe BuildCowTreatments.
            int? reasonId = random.Next(100) < 60
                ? reasonIds[random.Next(reasonIds.Count)]
                : null;

            planned.Add(new PlannedCowTreatment(
                0,
                cowIds[random.Next(cowIds.Count)],
                medicineIds[random.Next(medicineIds.Count)],
                today.AddDays(random.Next(-6, 15)),
                Dosage(random),
                whereHow.WhereHowId,
                isFound,
                isFound && random.Next(100) < 50,
                whereHow.ShowDialog ? quarterIds[random.Next(quarterIds.Count)] : noQuarter,
                reasonId));
        }

        return planned;
    }

    /// <summary>15 geplante Klauenbehandlungen mit kurzer Notiz.</summary>
    private static List<PlannedClawTreatment> BuildPlannedClawTreatments(
        Random random, DateTime today, IReadOnlyList<string> cowIds)
    {
        var planned = new List<PlannedClawTreatment>(15);

        for (var i = 0; i < 15; i++)
        {
            var entry = new PlannedClawTreatment
            {
                EarTagNumber = cowIds[random.Next(cowIds.Count)],
                TreatmentDate = today.AddDays(random.Next(-4, 21)),
                Desciption = PlannedClawNotes[random.Next(PlannedClawNotes.Length)]
            };

            foreach (var position in HoofPositions.All
                         .OrderBy(_ => random.Next())
                         .Take(1 + random.Next(2)))
            {
                entry.SetFlag(position, true);
            }

            planned.Add(entry);
        }

        return planned;
    }

    // ---- Hilfsfunktionen ------------------------------------------------

    private static List<int> QuarterIds(IReadOnlyDictionary<(bool, bool, bool, bool), int> udders)
        => udders
            .Where(u => u.Key != (false, false, false, false))
            .Select(u => u.Value)
            .OrderBy(id => id)
            .ToList();

    private static float Dosage(Random random)
        => (float)Math.Round(5 + random.NextDouble() * 25, 1);

    /// <summary>
    /// Verteilt <paramref name="total"/> Termine rueckwaerts ueber
    /// <paramref name="months"/> Monate. Die Gewichte haengen am Monat des
    /// Jahres und wiederholen sich, damit das Jahresdiagramm eine erkennbare
    /// Kurve bekommt statt zwoelf gleich hoher Balken.
    /// </summary>
    private static IEnumerable<DateTime> SpreadOverMonths(
        Random random, DateTime today, int months, int total)
    {
        // Erfunden, aber nicht flach: Klauenpflege haeuft sich im Fruehjahr
        // und Herbst, Eutersachen im Sommer.
        int[] weights = { 5, 4, 7, 9, 8, 11, 12, 10, 9, 8, 6, 5 };

        var buckets = new List<(DateTime Start, int Weight)>(months);
        var weightSum = 0;

        for (var back = months - 1; back >= 0; back--)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-back);
            var weight = weights[monthStart.Month - 1];
            buckets.Add((monthStart, weight));
            weightSum += weight;
        }

        foreach (var (start, weight) in buckets)
        {
            var count = Math.Max(1, (int)Math.Round((double)total * weight / weightSum));
            var daysInMonth = DateTime.DaysInMonth(start.Year, start.Month);

            for (var i = 0; i < count; i++)
            {
                var candidate = start.AddDays(random.Next(daysInMonth));

                // Der laufende Monat ist nur bis heute gefuellt: eine
                // Behandlung mit Datum in der Zukunft gehoert in eine der
                // beiden Planungstabellen, nicht hierher.
                if (candidate > today)
                {
                    candidate = today.AddDays(-random.Next(3));
                }

                yield return candidate;
            }
        }
    }
}
