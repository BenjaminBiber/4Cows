# Mehrfachauswahl zum Entfernen von Verbänden

Datum: 2026-09-14
Status: freigegeben, in Umsetzung

## Problem

Die Seite `/Verband_Daten` (`Remove_Claw_Bandage_Table.razor`) listet jeden offenen
Verband als eigene Zeile und bietet pro Zeile einen Knopf „Entfernen". Nach einem
Klauenschnitt-Durchgang stehen dort regelmäßig zwanzig und mehr überfällige Verbände.
Jeder einzelne kostet heute Klick, Bestätigungsdialog, Klick — und danach rendert die
Tabelle neu, sodass die nächste Zeile woanders steht.

Gesucht ist eine Mehrfachauswahl: mehrere Verbände anhaken, einmal bestätigen, alle
als entfernt markieren.

## Randbedingung: eine Zeile ist keine Behandlung

Das ist die Eigenheit, an der sich der ganze Entwurf ausrichtet. `BandagedClaw` ist
eine reine Anzeige-Projektion: eine Zeile pro bandagierter Klaue. Die Datenbank kennt
aber nur **ein gemeinsames `IsBandageRemoved` je `ClawTreatment`**. Eine Behandlung
mit Verbänden an zwei Klauen erzeugt zwei Zeilen, die sich nicht getrennt entfernen
lassen.

Der heutige Einzel-Entfernen-Dialog löst das durch Erklären („beim Entfernen werden
ALLE 2 Verbände als entfernt markiert"). Für eine Mehrfachauswahl reicht das nicht:
bei zwölf angehakten Zeilen aus sieben Behandlungen kann niemand vorhersagen, was
zusätzlich verschwindet.

## Entscheidungen

1. **Der Auswahlzustand ist eine Menge von `TreatmentId`, nicht von Zeilenschlüsseln.**
   `HashSet<int> _selected`, und `IsSelected(row) => _selected.Contains(row.TreatmentId)`.
   Damit färbt eine markierte Behandlung von selbst alle ihre Klauen-Zeilen, und das
   Mitmarkieren der Geschwister braucht keine eigene Logik — es fällt aus dem Datentyp
   heraus. Eine Menge von `row.Key` hätte die Kopplung an zwei Stellen nachbilden
   müssen (beim Anhaken und beim Entfernen) und wäre irgendwann auseinandergelaufen.
2. **Checkboxen sind immer sichtbar**, kein Auswahlmodus zum Einschalten. Der
   Hauptfall ist „nach Überfällig filtern, alles weghaken" — ein vorgeschalteter
   Moduswechsel kostete bei jedem Durchgang einen zusätzlichen Klick.
3. **Kopf-Checkbox gibt es nicht, „Alle auswählen" steht in der Aktionsleiste.**
   `MeadowTable` rendert Spaltenköpfe als reinen Text (`column.Title`), eine Checkbox
   dort hinein bekäme man nur mit einem weiteren Parameter. Die Aktionsleiste kann es
   ohnehin besser: sie bezieht sich auf **alle gefilterten** Zeilen, auch die auf
   Seite 2, und kann die Zahl benennen („Alle 37 auswählen").
4. **Filter-Wechsel löscht die Auswahl nicht.** Wer sich vertippt, verliert sonst eine
   mühsam zusammengeklickte Auswahl. Ausgeblendete, aber ausgewählte Zeilen werden im
   Zähler ehrlich benannt („4 ausgewählt, 1 davon ausgeblendet").
5. **Ein Sammel-UPDATE statt N Einzelaufrufe.** Neue Servicemethode
   `RemoveBandagesAsync(IReadOnlyCollection<int>)` mit einem `ExecuteUpdateAsync` über
   `Where(t => ids.Contains(t.ClawTreatmentId))`. `RemoveBandageAsync` N-mal zu rufen
   hieße N DbContexts, N Runden und bei einem Fehler in der Mitte einen halb
   entfernten Stapel. Die bestehende Einzelmethode bleibt unangetastet — Kuh-Detail
   und der Zeilenknopf nutzen sie weiter.
6. **Die Auswahl-Logik gehört der Seite, nicht `MeadowTable`.** Die Tabelle ist bereits
   rund 450 Zeilen und trägt Sortierung und Blättern für neun Seiten. Sie bekommt genau
   einen neuen Parameter (`RowClass`); alles andere — Checkbox-Spalte, Aktionsleiste,
   Mobil-Checkbox — entsteht mit Mitteln, die es heute schon gibt.
7. **Keine Unit-Tests, Nachweis an der laufenden App.** Es gibt kein bUnit und keine
   Testabdeckung für FE-Projektionen; `BandagedClaw` liegt in `4Cows-FE`, das
   Testprojekt referenziert nur `BBCowDataLibrary`. Die Kopplung ist durch Entscheidung 1
   strukturell erzwungen, nicht durch Logik, die schiefgehen könnte.

## Oberfläche

### Aktionsleiste

Sitzt zwischen `MeadowToolbar` und `MeadowTable` und erscheint erst, wenn etwas
ausgewählt ist:

```
☑ 4 Verbände ausgewählt (2 Behandlungen)   [Alle 37 auswählen] [Auswahl aufheben] [Verbände entfernen]
```

- Zähler nennt Verbände **und** Behandlungen, sobald die Zahlen auseinandergehen.
- Sind ausgewählte Zeilen weggefiltert: Zusatz „, 1 davon ausgeblendet".
- Sind bereits alle gefilterten Zeilen gewählt, entfällt „Alle auswählen".
- Mobil klebt die Leiste unten (`position: sticky`), damit sie beim Scrollen durch
  40 Karten erreichbar bleibt.

### Desktop-Tabelle

Neue erste Spalte, `Width = "44px"`, leerer Titel, kein `SortBy`. Inhalt ist eine
native `input[type="checkbox"]` im vorhandenen `.mw-check`-Stil (`accent-color:
var(--mw-primary)`) — derselbe Weg wie `MeadowMultiSelect` und `ChartEditorDialog`,
kein Nachbau und keine `MudCheckBox`. `aria-label` nennt Halsband und Klaue, damit
eine Spalte namenloser Kästchen vorlesbar bleibt.

Markierte Zeilen tragen `.mw-table__row--selected` (Grund `--mw-soft`).

### Mobil-Karten

Checkbox links in `.mw-row-card__top`, neben der Halsband-Nummer. Der bisherige
„Entfernen"-Knopf je Karte **bleibt**: für einen einzelnen Verband ist er schneller
als Anhaken plus Leiste. Markierte Karten tragen `.mw-row-card--selected`.

### Bestätigung

Derselbe `GenericDialog` wie heute, `ColorScheme = Color.Error`:

> „12 Verbände an 7 Behandlungen als entfernt markieren? Das betrifft die Halsbänder
> 104, 231, 340 und 4 weitere."

Bei genau einer ausgewählten Behandlung übernimmt der Dialog wörtlich den heutigen
Einzelfall-Text („Verband LV · Vorne links bei Halsband 104 als entfernt markieren?"
bzw. dessen Geschwister-Variante). Ab zwei Behandlungen gilt der Sammeltext oben. Dass
Geschwister mitgezogen wurden, steht bereits im Zähler der Aktionsleiste und wird im
Dialog nicht wiederholt.

## Technik

### `MeadowTable<TItem>`

```csharp
/// <summary>Zusätzliche Klasse pro Zeile, z.B. für Auswahl-Hervorhebung.</summary>
[Parameter] public Func<TItem, string?>? RowClass { get; set; }
```

Gerendert an beiden `<tr>`-Zweigen (mit und ohne `RowHref`), zusammengefügt mit der
bestehenden `mw-table__row--link`. `null` bedeutet keine Klasse; die anderen acht
Tabellen rendern unverändert.

### `ClawTreatmentService`

```csharp
public async Task<int> RemoveBandagesAsync(IReadOnlyCollection<int> ids)
```

Ein Kontext, ein `ExecuteUpdateAsync` — entweder alle oder keiner. Danach wird der
Cache für die betroffenen IDs in einem `SetItems` nachgezogen, analog zur
Einzelmethode. Rückgabe ist die Zahl der betroffenen Behandlungen; eine leere
Eingabe gibt `0` zurück, ohne die Datenbank anzufassen. Fehler werden wie überall
über `_databaseStatusService.ReportFailure()` und `LoggerService.LogError` gemeldet
und als `0` zurückgegeben.

### `Remove_Claw_Bandage_Table.razor`

Neu:

- `HashSet<int> _selected`
- `IsSelected`, `ToggleRow`, `SelectAllFiltered`, `ClearSelection`
- `SelectedRows` — alle Zeilen aus `_all`, deren `TreatmentId` markiert ist
  (bewusst `_all`, nicht `_rows`: ausgeblendete Geschwister zählen mit)
- `RemoveSelected()` — Dialog, `RemoveBandagesAsync`, Snackbar, Auswahl leeren,
  `Rebuild()`
- Aufräumen in `Rebuild()`: `_selected` wird auf die noch offenen `TreatmentId`s
  eingeschränkt, damit eine anderswo entfernte Behandlung nicht als Karteileiche in
  der Auswahl hängen bleibt.

Fehlerfall: Snackbar `Severity.Error`, **Auswahl bleibt stehen** — sonst müsste der
Nutzer zwölf Zeilen neu zusammenklicken.

### CSS (`meadow-components.css`)

Neu, ohne bestehende Regeln anzufassen: `.mw-table__row--selected`,
`.mw-row-card--selected`, `.mw-selbar`, `.mw-selbar__count`, `.mw-selbar__actions`
und die Sticky-Variante unter der 1024px-Grenze.

## Nicht in diesem Umfang

- Mehrfachauswahl in anderen Tabellen (Cow_Table, Claw_Table). `RowClass` macht es
  später möglich, aber es gibt heute keinen Bedarf.
- Umbauen des Datenmodells auf ein `IsBandageRemoved` je Klaue. Das wäre die
  eigentliche Ursache, ist aber eine Migration mit Auswirkung auf Auswertungen,
  Demo-Seed und Transferskript — ein eigenes Vorhaben.
- Auswahl über Seitenwechsel und Neuladen hinaus zu erhalten.

## Prüfung

Gegen die Dev-Datenbank an der laufenden App:

1. Mehrere Verbände anhaken, darunter eine Behandlung mit zwei bandagierten Klauen —
   die zweite Zeile muss sich mitmarkieren und der Zähler „(n Behandlungen)" nennen.
2. „Nur überfällig" einschalten, „Alle auswählen", entfernen — die Tabelle muss
   danach nur noch nicht-überfällige Verbände zeigen.
3. In der Datenbank prüfen, dass `IsBandageRemoved` für genau die betroffenen
   `ClawTreatmentId`s steht und für keine weitere.
4. Auswählen, danach Suchbegriff eingeben: Auswahl bleibt, Zähler meldet
   ausgeblendete Zeilen.
5. Mobil (375px): Karten anhaken, Sticky-Leiste erreichbar, Einzel-„Entfernen" wirkt
   weiterhin.
6. Cow_Table und Kuh-Detailseite gegenprüfen — `RowClass` darf dort nichts ändern.
