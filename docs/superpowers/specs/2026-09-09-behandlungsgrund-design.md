# Optionaler Behandlungsgrund für Kuh- und geplante Kuhbehandlungen

Datum: 2026-09-09
Status: freigegeben, in Umsetzung

## Problem

Eine Kuhbehandlung dokumentiert heute *was* verabreicht wurde (`Medicine`), *wie und
wo* (`WhereHow` plus `Udder`), *wann* und *wie viel*. Warum behandelt wurde, steht
nirgends. Mastitis, Lahmheit und eine Routineimpfung sind in der Tabelle nicht
auseinanderzuhalten, und es gibt keine Auswertung, die nach dem Anlass fragt.

Der Grund soll erfassbar sein, aber **optional** bleiben: im Stall entsteht der
Eintrag oft unter Zeitdruck, und ein Pflichtfeld würde entweder blockieren oder mit
Platzhaltern gefüllt.

## Entscheidungen

1. **Nachschlagetabelle statt Freitext.** `Treatment_Reason` mit eigener ID, gebaut
   wie `Medicine`. Freitext würde durch Tippfehler beliebig viele Varianten desselben
   Grundes erzeugen und den gewünschten Filter wertlos machen.
2. **Anlegen beim Tippen.** Ein unbekannter Grund wird beim Speichern angelegt —
   derselbe Weg wie `MedicineService.GetMedicineIdByName` und
   `WhereHowService.GetWhereHowIDByName`. Eine reine Auswahlliste ohne Neuanlage wäre
   im Stall unbrauchbar.
3. **Frische Installationen starten leer.** Kein Seed mit Standardgründen: jeder
   Betrieb bekommt genau seine eigenen Begriffe statt ungenutzter Karteileichen.
   Nur die Demo-Instanz bekommt Beispieldaten.
4. **Nullable statt Sentinel.** `Treatment_Reason_ID` ist `int?`. `UdderId` benutzt
   `int.MinValue` als „nicht gesetzt", aber dort ist der Wert je nach `WhereHow`
   verpflichtend; hier ist „kein Grund" ein regulärer, dauerhafter Zustand. `NULL`
   ist die ehrliche Abbildung, und bestehende Zeilen erhalten ihn durch die Migration
   von selbst.
5. **Ein Grund je Eingabemaske, nicht je Tier.** Wie Datum, Medikament, Menge und
   Wie/Wo gilt der Grund für alle Zeilen des Dialogs. Nur Halsband und Ohrmarke sind
   je Tier verschieden.
6. **Das Suchfeld der Tabellen bleibt unverändert.** Der Grund ist über den Filter
   erreichbar; ihn zusätzlich in die Freitextsuche zu nehmen, würde deren Treffer
   verwässern.
7. **Kein Fremdschlüssel.** Das Schema aus `20251223183944_InitialCreate` legt
   keine an; der Service prüft beim Löschen selbst nach, wie `WhereHowService`.

## Datenmodell

### Neue Tabelle

```
Treatment_Reason
  Treatment_Reason_ID    int, PK, auto increment
  Treatment_Reason_Name  varchar(64), NOT NULL
```

`varchar(64)` wie `Medicine.Medicine_Name` — nicht das `longtext`, das `WhereHow`
mangels `StringLength` bekommt.

### Geänderte Tabellen

| Tabelle | Neue Spalte | Typ |
| --- | --- | --- |
| `Cow_Treatment` | `Treatment_Reason_ID` | `int NULL` |
| `Planned_Cow_Treatment` | `Treatment_Reason_ID` | `int NULL` |

Klauenbehandlungen bleiben unberührt.

### Migration

`AddTreatmentReason`: ein `CreateTable` plus zwei `AddColumn`. Läuft beim nächsten
Start über das vorhandene `MigrateAsync()` in `Program.cs:108`. Bestehende Zeilen
erhalten `NULL`, kein Datenverlust; `Down` entfernt Spalten und Tabelle wieder.

`DatabaseContext` bekommt `public DbSet<TreatmentReason> TreatmentReasons`.

## `TreatmentReasonService`

Neu in `BBCowDataLibrary/Services/`, Aufbau wie `WhereHowService`: Singleton mit
`ImmutableDictionary<int, TreatmentReason>`-Cache, `IDbContextFactory`,
`DatabaseStatusService.ReportSuccess/ReportFailure` in jedem Pfad.

| Methode | Zweck |
| --- | --- |
| `GetAllDataAsync()` | Lädt den Cache neu. |
| `GetIdByNameAsync(name)` | Findet oder legt an. Vergleich getrimmt und case-insensitiv, gespeichert wird getrimmt — sonst steht „Mastitis " als eigener, optisch identischer Eintrag in der Liste. |
| `GetNameById(int? id)` | `"–"` bei `null` und bei unbekannter ID. |
| `GetUsageCountsAsync()` | Zwei serverseitige `GroupBy(...).Count()` über `Cow_Treatment` und `Planned_Cow_Treatment`, summiert. Liefert `null`, wenn die Zählung fehlschlägt — die Pflegeseite sperrt dann das Löschen, statt alles als unbenutzt auszuweisen. |
| `InsertDataAsync` / `UpdateDataAsync` | Wie bei `Medicine`; `UpdateDataAsync` per `ExecuteUpdateAsync` auf den Namen. |
| `MergeAsync(sourceId, targetId)` | Transaktion: `Treatment_Reason_ID` in beiden Behandlungstabellen umhängen, Quelle löschen. Guard gegen `source == target` — erreichbar über eine reine Groß-/Kleinschreibungsänderung, weil der Namensvergleich case-insensitiv ist. |
| `RemoveByIdAsync(id)` | Zählt in derselben Transaktion unmittelbar vor dem Delete nach und bricht ab, wenn der Eintrag inzwischen benutzt wird. |
| `SearchAsync(value, token)` | Für das Autocomplete, nach dem Muster von `CowTreatmentService.SearchCowTreatmentWhereHow`: leere Eingabe liefert alle Namen, eine Eingabe ohne Treffer liefert die Eingabe selbst, damit sie übernommen werden kann. |

Registrierung als Singleton in `Program.cs`, Laden in
`MeadowDataLoader.EnsureLookupsAsync()` — dort steht alles, was Spalten auflöst.

## Dialoge

`Add_Cow_Treatment_Dialog` und `Add_Planned_Cow_Treatment_Dialog` bekommen unter der
Zeile „Wie / Wo · Menge" ein Feld **„Behandlungsgrund"**: `MudAutocomplete` mit
`CoerceValue="true"` und `SelectValueOnTab="true"`, wie das Medikamentenfeld,
Platzhalter „optional".

Verhalten beim Speichern:

- Feld leer → `TreatmentReasonId = null`, **keine** Fehlermeldung. Der Dialog darf
  ohne Grund durchlaufen.
- Feld gefüllt → `GetIdByNameAsync`, bei Bedarf wird der Grund angelegt. Schlägt das
  fehl, bricht `Save` mit Snackbar ab, bevor die erste Behandlung geschrieben wird —
  dieselbe Reihenfolge wie bei Medikament und Wie/Wo, damit keine halb gespeicherte
  Serie zurückbleibt.

`Add_Cow_Treatment_Dialog` liest in `OnInitialized` einen vorbelegten
`Cow_Treatment.TreatmentReasonId` und zeigt den Namen an. Damit trägt
`Planned_Cow_Table.Complete()` den geplanten Grund in die abgeschlossene Behandlung
weiter; die Zuweisung wird dort beim Umbau `PlannedCowTreatment → CowTreatment`
ergänzt.

## Tabellen und Filter

### Spalten

`Cow_Table` und `Planned_Cow_Table` bekommen die Spalte **„Grund"** als letzte
Datenspalte — hinter „Menge", in der geplanten Tabelle vor der Aktionsspalte.
Sortiert wird über den aufgelösten Namen, nicht über die ID, die niemand sieht.
Leere Zellen zeigen `–`, also genau das, was `GetNameById(null)` liefert.

Mobil:

- `Cow_Table`: ein weiteres `mw-row-card__field` mit Label „Grund".
- `Planned_Cow_Table`: ein `<span>` in der bestehenden `mw-row-card__meta`-Zeile.

Auf den Karten entfällt die Angabe bei fehlendem Grund **ganz**, statt ein `–` in
den knappen Platz zu setzen. Die Karte prüft dafür selbst auf `null` und ruft
`GetNameById` nur im Trefferfall — der Desktop-Pfad bleibt der einzige, der das `–`
je zu sehen bekommt.

### Filter

`CowTableFilter` und `PlannedCowTableFilter` erhalten je ein
`HashSet<string> Reasons` (`OrdinalIgnoreCase`), das in `ActiveCount` als eine Gruppe
zählt und von `Reset()` geleert wird — analog zu `Medicines`.

Im Filter-Popover beider Tabellen ein `MeadowMultiSelect "Grund"`. Die Optionen
stammen aus den **tatsächlich vorkommenden** Gründen der geladenen Zeilen, nicht aus
dem vollständigen Stammdatenbestand — dieselbe Regel wie `MedicineOptions`.

**Option „Ohne Grund".** Der Liste wird ein fester Eintrag „Ohne Grund"
vorangestellt, sobald mindestens eine Zeile ohne Grund vorhanden ist.

Das ist **ausschließlich** eine Anzeigeoption der Filterleiste. Es entsteht dadurch
weder eine Zeile in `Treatment_Reason` noch ein Wert an einer Behandlung; die Option
wird nie an `GetIdByNameAsync` weitergereicht und ist außerhalb des Filters nirgends
sichtbar. Konkret:

- ausgewählt → es passen genau die Zeilen mit `TreatmentReasonId == null`;
- ODER-verknüpft mit den übrigen Auswahlen, wie innerhalb jeder Filtergruppe üblich
  („Mastitis **oder** ohne Grund");
- nicht ausgewählt → Zeilen ohne Grund fallen aus einem gesetzten Grund-Filter heraus,
  wie jede Zeile, die keinen gewählten Wert trägt;
- die Konstante liegt einmal in `TableFilters.cs` (`ReasonFilter.NoneOption`), damit
  Tabelle und Filter nicht mit zwei getrennten Literalen auseinanderdriften.

Legt jemand über den Behandlungs-Dialog einen echten Grund mit exakt diesem Namen an,
fällt er mit der Option in einen Haken zusammen und beide Mengen erscheinen gemeinsam.
Das ist unwahrscheinlich und harmlos; ein zweiter Datentyp im Filter wäre dafür nicht
gerechtfertigt.

## Basisdaten

Neuer Chip-Reiter **„Behandlungsgrund"** in `Settings.razor` (`BaseDataTabs`), Seite
`Components/4CowsComponent/BaseData/BaseDataTreatmentReason.razor` nach dem Vorbild
von `BaseDataWhereHow`:

- Suchfeld, `MeadowTable` (dicht, ohne Mobil-Karten) mit ID / Bezeichnung /
  Verwendungen / Aktionen
- Duplikate werden mit dem Pill „doppelt" markiert
- Papierkorb nur aktiv bei Verwendungen `0`; ist die Zählung `null`, bleibt er
  gesperrt
- Fußzeile „Behandlungsgrund hinzufügen"

Dazu `Components/4CowsComponent/Dialogs/BaseData/EditTreatmentReasonDialog.razor`
nach `EditMedicineDialog`: `MudForm` mit Pflicht-Namensfeld, gearbeitet wird auf einer
Kopie und nicht auf der Instanz aus dem Service-Cache. Trifft der neue Name einen
bestehenden Eintrag, fragt der Dialog nach und ruft `MergeAsync` — sonst
`UpdateDataAsync`. Ohne diesen Weg wäre ein bereits benutzter Tippfehler-Eintrag
weder löschbar noch korrigierbar.

## Demo-Daten

`DemoDataSeeder` legt eine Handvoll Gründe an (Mastitis, Lahmheit, Fieber,
Nachgeburtsverhaltung, Trockenstellen, Impfung) und setzt sie bei einem **Teil** der
erzeugten Behandlungen und Planungen. Ein Teil bleibt bewusst ohne Grund, damit die
Demo beide Zustände zeigt — einschließlich des Filters „Ohne Grund". Wie bei
Medikamenten und Wie/Wo nur, wenn die Tabelle noch leer ist.

`DataSeeder` bleibt unverändert: produktive Installationen starten mit leerer Liste.

Ergänzung aus der Umsetzung: `Treatment_Reason` gehört auch in
`DemoResetBackgroundService.TablesToClear`. Die Liste leert nachts unter anderem
`Medicine` und `WhereHow`, damit die öffentliche Demo nicht mit Begriffen zuwächst,
die Besucher eingetippt haben — für die Gründe gilt derselbe Grund, und sie
entstehen auf demselben Weg. Die Tabelle steht am Ende der Liste, bei den übrigen
Nachschlagetabellen.

## Prüfung

Das Testprojekt enthält ausschließlich reine Logik-Tests
(`BBCowDataLibrary.Tests/Kpi`) und keinerlei datenbankgestützte Infrastruktur — für
den Service entstehen deshalb keine automatisierten Tests.

Die Filterlogik ist ebenfalls nicht automatisiert prüfbar, weil `TableFilters.cs` im
Frontend-Projekt liegt, auf das das Testprojekt nicht verweist. Das bleibt so; es für
diese Änderung umzubauen wäre Refactoring ohne Bezug zum Ziel.

Verifiziert wird über `dotnet build` und einen Durchlauf gegen die Demo-Instanz:

1. Behandlung ohne Grund speichern → Zeile erscheint mit `–`.
2. Behandlung mit neuem Grund speichern → Grund erscheint in der Spalte und
   anschließend im Autocomplete.
3. Behandlung planen, dann „Behandlung abschließen" → der Grund steht im
   Abschluss-Dialog vorbelegt und landet in der Kuh-Tabelle.
4. Filter „Grund" mit einem Wert, mit mehreren, mit „Ohne Grund" und in Kombination;
   Zähler-Badge und Zurücksetzen prüfen.
5. Basisdaten: umbenennen, auf einen bestehenden Namen umbenennen (Merge),
   unbenutzten Eintrag löschen, benutzten Eintrag nicht löschen können.
6. Mobil-Ansicht beider Tabellen.

## Bewusst nicht enthalten

- Behandlungsgrund für Klauenbehandlungen und geplante Klauenbehandlungen.
- Nachträgliches Bearbeiten bestehender Behandlungen, um einen Grund zu ergänzen —
  dafür gibt es in der Anwendung generell keinen Weg.
- KPI-Kacheln oder Auswertungen nach Grund. Erst braucht es Daten.
- Aufnahme des Grundes in die Freitextsuche der Tabellen.
