using BB_Cow.Class;
using BB_Cow.Services;
using MudBlazor;

namespace _4Cows_FE.Components.Meadow;

/// <summary>
/// Eine Medikamentenzeile im Behandlungs-Dialog: Medikament, Menge, Wie/Wo und
/// - wenn das Wie/Wo die Viertel-Auswahl traegt - die gewaehlten Euterviertel.
///
/// Der Dialog haelt die Liste, MeadowMedicationRows rendert und mutiert sie -
/// wie MeadowMultiSelect mit der Auswahlmenge der Seite.
///
/// Bewusst class und NICHT record: die Zeile ist der Key im Wiederholer und das
/// Argument von List.Remove. Mit Wertegleichheit wuerde der Papierkorb der
/// dritten leeren Zeile die erste entfernen.
///
/// Der Wohnort ist das Frontend und nicht CowInput.cs neben NumberEntry: die
/// drei Anzeige-Flags sind "welches Icon wird gerendert", und das muss die
/// Datenbibliothek nicht wissen.
/// </summary>
public sealed class MedicationEntry
{
    public string? MedicineName { get; set; }
    public float Dosage { get; set; }

    /// <summary>Der eingegebene TEXT. Aufgeloest wird erst beim Speichern.</summary>
    public string WhereHowName { get; set; } = "";

    /// <summary>int.MinValue heisst "noch nichts gewaehlt" - vgl. UdderService.HasAnyQuarter.</summary>
    public int UdderId { get; set; } = int.MinValue;

    /// <summary>
    /// Einheit des gewaehlten Medikaments, nur Anzeige am Mengenfeld dieser
    /// Zeile. Je Zeile und nicht je Dialog: zwei Medikamente in einer Eingabe
    /// koennen "ml" und "Injektor" sein.
    /// </summary>
    public string DosageUnit { get; set; } = "";

    /// <summary>
    /// Ob der Wie/Wo-Wert DIESER Zeile aus der Vorbelegung stammt und nicht
    /// getippt wurde.
    ///
    /// Damit folgt ein Medikamentenwechsel der neuen Verabreichungsart, eine
    /// von Hand eingetragene bleibt aber stehen. Ohne dieses Merker-Flag waere
    /// nur eines von beidem moeglich: entweder ueberschreibt jeder Wechsel die
    /// Eingabe, oder die erste Vorbelegung bleibt fuer immer haengen.
    /// </summary>
    public bool WhereHowAutoFilled { get; set; }

    // Nur Anzeige. Geschrieben von MedicationRows.ApplyWhereHow und von
    // MeadowMedicationRows.SaveNewWhereHow.
    //
    // Bewusst gespeichert und nicht aus (WhereHowName, WhereHowService)
    // berechnet: SaveNewWhereHow setzt sie danach absichtlich abweichend, damit
    // der Euter-Knopf erreichbar bleibt, wenn der Viertel-Dialog abgebrochen
    // wird. Berechnete Werte wuerden genau diese Zusicherung stillschweigend
    // wieder loeschen.
    public bool ShowEditWhereHowOptions { get; set; }
    public bool ShowDialogForNewWhereHow { get; set; }
    public bool ShowUdderButton { get; set; }
}

/// <summary>
/// Eine Zeile mit aufgeloesten IDs. Entsteht KOMPLETT vor dem ersten Insert,
/// damit ein Fehlschlag keine halb gespeicherte Serie zuruecklaesst.
/// MedicineName wird nur fuer Meldungen mitgefuehrt.
/// </summary>
public sealed record ResolvedMedication(
    int MedicineId,
    string MedicineName,
    float Dosage,
    int WhereHowId,
    int UdderId);

/// <summary>
/// Die Logik hinter den Medikamentenzeilen, ohne Markup: das Setzen der
/// Anzeige-Flags nach einer Wie/Wo-Eingabe und das Aufloesen aller Zeilen beim
/// Speichern.
///
/// Statisch und nicht in MeadowMedicationRows, weil der Behandlungs-Dialog beim
/// Abschliessen einer Planung eine Zeile vorbelegt, BEVOR die Komponente
/// existiert - und der Euter-Knopf dann sofort da sein muss. Damit braucht
/// keiner der beiden Dialoge eine Referenz auf die Komponente.
/// </summary>
public static class MedicationRows
{
    /// <summary>
    /// Setzt Text und Anzeige-Flags einer Zeile nach einer Eingabe im
    /// Wie/Wo-Feld.
    /// </summary>
    public static void ApplyWhereHow(
        MedicationEntry entry, string? value, WhereHowService whereHowService)
    {
        entry.WhereHowName = value ?? "";
        var text = entry.WhereHowName.Trim();

        // Jede Aenderung gilt zunaechst als getippt. ApplyMedicineDefaults
        // setzt das Flag direkt nach seinem Aufruf wieder auf true.
        entry.WhereHowAutoFilled = false;

        var known = whereHowService.WhereHows.Values
            .FirstOrDefault(w => string.Equals(
                w.WhereHowName.Trim(), text, StringComparison.OrdinalIgnoreCase));

        entry.ShowUdderButton = known?.ShowDialog ?? false;
        entry.ShowEditWhereHowOptions = known is null && text.Length > 0;

        // Ohne Viertel-Auswahl darf keine haengenbleiben: wer erst "Euter" mit
        // LV waehlt und dann auf "i.m." wechselt, speicherte sonst die Viertel
        // der verworfenen Auswahl mit.
        if (!entry.ShowUdderButton)
        {
            entry.UdderId = int.MinValue;
        }
    }

    /// <summary>
    /// Uebernimmt Einheit und Standard-Verabreichungsart des Medikaments in
    /// die Zeile - die Verabreichungsart aber nur in ein leeres oder selbst
    /// vorbelegtes Feld. Eine von Hand eingetragene Verabreichungsart darf ein
    /// spaeter korrigiertes Medikament nicht wegwerfen.
    /// </summary>
    public static void ApplyMedicine(
        MedicationEntry entry,
        string? medicineName,
        MedicineService medicineService,
        WhereHowService whereHowService,
        SettingsService settingsService)
    {
        entry.MedicineName = medicineName;
        entry.DosageUnit = ResolveUnit(medicineName, medicineService, settingsService);

        if (!string.IsNullOrWhiteSpace(entry.WhereHowName) && !entry.WhereHowAutoFilled)
        {
            return;
        }

        var medicine = FindMedicine(medicineName, medicineService);
        if (medicine?.DefaultWhereHowId is not { } whereHowId)
        {
            return;
        }

        var name = whereHowService.GetWhereHowNameById(whereHowId);
        if (string.IsNullOrWhiteSpace(name))
        {
            // Die Zeile wurde inzwischen geloescht - ohne Fremdschluessel
            // haelt die Datenbank das nicht auf.
            return;
        }

        // Ueber ApplyWhereHow, weil dort der Euter-Knopf und das Zuruecksetzen
        // der Viertel haengen. Bei "IZ" muss die Viertel-Auswahl sofort
        // erscheinen - und die Zeile gilt damit bis zur Auswahl als
        // unvollstaendig. Das ist beabsichtigt: ein intrazitzenales Praeparat
        // *soll* das Viertel verlangen.
        ApplyWhereHow(entry, name, whereHowService);
        entry.WhereHowAutoFilled = true;
    }

    /// <summary>
    /// Einheit des Medikaments, sonst die Voreinstellung. Kein "ml" als
    /// Rueckfall im Code - eine Tablette in Millilitern auszuweisen waere
    /// schlechter als die konfigurierte Einheit zu zeigen.
    /// </summary>
    public static string ResolveUnit(
        string? medicineName, MedicineService medicineService, SettingsService settingsService)
    {
        var medicine = FindMedicine(medicineName, medicineService);

        return string.IsNullOrWhiteSpace(medicine?.DosageUnit)
            ? settingsService.DefaultDosageUnit
            : medicine!.DosageUnit!.Trim();
    }

    private static Medicine? FindMedicine(string? medicineName, MedicineService medicineService)
    {
        if (string.IsNullOrWhiteSpace(medicineName))
        {
            return null;
        }

        return medicineService.Medicines.Values.FirstOrDefault(m =>
            string.Equals(m.MedicineName.Trim(), medicineName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Prueft und loest ALLE Zeilen auf, bevor der Aufrufer irgendetwas
    /// schreibt. null heisst abbrechen - die Meldung ist dann schon gezeigt,
    /// denn nur hier ist die Nummer der fehlerhaften Zeile bekannt.
    ///
    /// Aufloesen ist NICHT nebenwirkungsfrei: GetMedicineIdByName und
    /// GetWhereHowIDByName legen unbekannte Eintraege an - genau das kauft
    /// CoerceValue an den Feldern. Ein Abbruch in Zeile 3 laesst also ein neu
    /// angelegtes Medikament aus Zeile 1 zurueck. Das ist heute genauso und ist
    /// harmlos: Stammdaten mit eigener Pflegeseite, keine Behandlungen. Was
    /// zaehlt, ist die Zusicherung dahinter - es entsteht keine halb
    /// gespeicherte Behandlungsserie.
    /// </summary>
    public static async Task<List<ResolvedMedication>?> ResolveAsync(
        IReadOnlyList<MedicationEntry> rows,
        MedicineService medicineService,
        WhereHowService whereHowService,
        UdderService udderService,
        ISnackbar snackbar)
    {
        if (rows.Count == 0)
        {
            snackbar.Add("Bitte mindestens ein Medikament angeben.", Severity.Error);
            return null;
        }

        var resolved = new List<ResolvedMedication>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            // Nur bei mehreren Zeilen wird die Zeile benannt: bei einer bleiben
            // die Meldungen Wort fuer Wort die von vorher. Und nicht "Zeile 2" -
            // der Dialog hat jetzt zwei Wiederholer, Tiere und Medikamente, da
            // waere "Zeile" zweideutig.
            var prefix = rows.Count > 1 ? $"{i + 1}. Medikament: " : "";

            if (string.IsNullOrWhiteSpace(row.MedicineName))
            {
                snackbar.Add($"{prefix}Bitte ein Medikament angeben.", Severity.Error);
                return null;
            }

            // Neu. Ein leeres Wie/Wo lief bisher in GetWhereHowIDByName("") und
            // legte dort einen WhereHow OHNE Namen an, der danach in jedem
            // Wie/Wo-Autocomplete stand.
            if (string.IsNullOrWhiteSpace(row.WhereHowName))
            {
                snackbar.Add($"{prefix}Bitte ein Wie / Wo angeben.", Severity.Error);
                return null;
            }

            // Bisher prueften das nur die geplanten Behandlungen. Nicht == 0,
            // sondern <= 0: MudNumericField hat kein Min, eine negative Menge
            // ist eingebbar und ist keine Menge.
            if (row.Dosage <= 0)
            {
                snackbar.Add($"{prefix}Bitte eine Menge angeben.", Severity.Error);
                return null;
            }

            var medicineId = await medicineService.GetMedicineIdByName(row.MedicineName);
            if (medicineId == int.MinValue)
            {
                snackbar.Add($"{prefix}Medikament nicht gefunden!", Severity.Error);
                return null;
            }

            var whereHowId = await whereHowService.GetWhereHowIDByName(
                row.WhereHowName, row.ShowDialogForNewWhereHow);

            if (whereHowId == int.MinValue)
            {
                snackbar.Add($"{prefix}Wie / Wo nicht gefunden!", Severity.Error);
                return null;
            }

            // Traegt das Wie/Wo die Viertel-Auswahl, ist "kein Viertel" keine
            // gueltige Angabe. Ohne diese Pruefung fiele der Aufrufer still auf
            // GetIdForNoQuarters() zurueck: wer den Euter-Knopf nie antippte
            // oder den Viertel-Dialog abbrach, speicherte eine Euterbehandlung
            // ohne Viertel - in der Tabelle nur an einem fehlenden "(LV)" zu
            // erkennen.
            //
            // ShowDialog wird am AUFGELOESTEN Eintrag gelesen, nicht am Toggle
            // der Zeile: der gilt nur fuer neu angelegte Wie/Wos, und
            // new WhereHow() hat ShowDialog = true.
            var whereHow = whereHowService.GetById(whereHowId);
            if (whereHow.ShowDialog && !udderService.HasAnyQuarter(row.UdderId))
            {
                snackbar.Add(
                    $"{prefix}„{whereHow.WhereHowName}\" braucht mindestens ein Euterviertel.",
                    Severity.Error);
                return null;
            }

            var udderId = row.UdderId == int.MinValue
                ? await udderService.GetIdForNoQuarters()
                : row.UdderId;

            resolved.Add(new ResolvedMedication(
                medicineId, row.MedicineName.Trim(), row.Dosage, whereHowId, udderId));
        }

        // Verglichen wird auf den AUFGELOESTEN IDs, nicht auf dem Text: sonst
        // gingen "Baytril" und "baytril " als zwei Zeilen durch. Zwei Zeilen mit
        // demselben Tripel ergeben zwei in der Tabelle nicht unterscheidbare
        // Behandlungen und zaehlen in jeder KPI doppelt.
        //
        // Dasselbe Medikament mit anderem Wie/Wo oder anderen Vierteln bleibt
        // still erlaubt - eins ins Euter, eins i.m. ist eine echte Kombination.
        var duplicate = resolved
            .GroupBy(m => (m.MedicineId, m.WhereHowId, m.UdderId))
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            snackbar.Add(
                $"„{duplicate.First().MedicineName}\" steht zweimal mit gleichem Wie / Wo. "
                + "Bitte eine Zeile entfernen.",
                Severity.Error);
            return null;
        }

        return resolved;
    }
}
